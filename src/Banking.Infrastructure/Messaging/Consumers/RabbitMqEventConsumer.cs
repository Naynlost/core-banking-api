using System.Diagnostics;
using System.Text;
using Banking.Application.Abstractions;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Banking.Infrastructure.Messaging.Consumers;

// Broker en az bir kez teslim eder; işlenen mesaj id'si inbox tablosuna yazılır ve tekrar teslimat işlenmeden ack'lenir
internal abstract class RabbitMqEventConsumer(
    RabbitMqConnectionProvider connections,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger logger) : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    // Aynı zamanda kuyruk adı ve inbox consumer id'si olarak kullanılır
    protected abstract string ConsumerName { get; }

    protected abstract string RoutingKey { get; }

    protected abstract Task HandleAsync(string eventType, string payload, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeUntilChannelClosesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception, "{Consumer} lost its broker connection; reconnecting in {Delay}",
                    ConsumerName, ReconnectDelay);
            }

            try
            {
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ConsumeUntilChannelClosesAsync(CancellationToken stoppingToken)
    {
        var connection = await connections.GetConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await MessageTopology.DeclareQueueAsync(channel, ConsumerName, RoutingKey, stoppingToken);
        await channel.BasicQosAsync(0, prefetchCount: 10, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => OnDeliveryAsync(channel, delivery, stoppingToken);
        await channel.BasicConsumeAsync(ConsumerName, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("{Consumer} is consuming queue {Queue}", GetType().Name, ConsumerName);

        while (!stoppingToken.IsCancellationRequested && channel.IsOpen)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task OnDeliveryAsync(
        IChannel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        // Publisher'ın mesaja koyduğu trace/correlation'ı devam ettirir
        using var activity = MessagingDiagnostics.ActivitySource.StartActivity(
            $"consume {ConsumerName}",
            ActivityKind.Consumer,
            MessagingDiagnostics.ParseTraceParent(ReadHeader(delivery, MessagingDiagnostics.TraceParentHeader)));
        var correlationId = ReadHeader(delivery, MessagingDiagnostics.CorrelationIdHeader);
        activity?.SetTag("messaging.destination.name", ConsumerName);
        using var scope = correlationId is null
            ? null
            : logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }) as IDisposable;

        try
        {
            var eventType = delivery.BasicProperties.Type ?? string.Empty;
            var payload = Encoding.UTF8.GetString(delivery.Body.ToArray());

            if (Guid.TryParse(delivery.BasicProperties.MessageId, out var messageId))
            {
                if (await AlreadyProcessedAsync(messageId, cancellationToken))
                {
                    logger.LogInformation(
                        "{Consumer} skipped duplicate message {MessageId}", ConsumerName, messageId);
                    await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
                    return;
                }

                await HandleAsync(eventType, payload, cancellationToken);
                await MarkProcessedAsync(messageId, cancellationToken);
            }
            else
            {
                // Mesaj id'si yok, dedupe imkansız — olduğu gibi işle
                await HandleAsync(eventType, payload, cancellationToken);
            }

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Bir kez requeue geçici hataları tolere eder; ikinci hatada mesaj poison sayılıp
            // banking.dead-letters'a düşer ve bir insanı bekler
            var requeue = !delivery.Redelivered;
            logger.LogError(
                exception, "{Consumer} failed to process delivery {DeliveryTag}; {Outcome}",
                ConsumerName, delivery.DeliveryTag,
                requeue ? "requeued for one retry" : "dead-lettered to " + MessageTopology.DeadLetterQueue);

            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue, cancellationToken);
        }
    }

    private static string? ReadHeader(BasicDeliverEventArgs delivery, string name)
    {
        if (delivery.BasicProperties.Headers is { } headers && headers.TryGetValue(name, out var value))
        {
            return value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes), // AMQP short string'ler byte olarak gelir
                string text => text,
                _ => null,
            };
        }

        return null;
    }

    private async Task<bool> AlreadyProcessedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        return await context.Set<InboxMessage>()
            .AnyAsync(m => m.Consumer == ConsumerName && m.MessageId == messageId, cancellationToken);
    }

    private async Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        await context.Set<InboxMessage>().AddAsync(
            new InboxMessage
            {
                Consumer = ConsumerName,
                MessageId = messageId,
                ProcessedAt = timeProvider.GetUtcNow(),
            },
            cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // Eş zamanlı bir tekrar teslimat bizden önce davranmış, zaten işaretlenmiş olması sorun değil
        }
    }
}
