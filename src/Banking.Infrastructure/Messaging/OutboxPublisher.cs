using System.Diagnostics;
using System.Text;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Banking.Infrastructure.Messaging;

/// <summary>
/// Polls the outbox and pushes pending events to the broker with publisher
/// confirms. A row is marked processed only after the broker confirmed it, so a
/// crash anywhere in between means the row stays pending and is retried — the
/// event is delivered at least once, never lost. Duplicates are the consumers'
/// inbox's problem.
/// </summary>
internal sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    RabbitMqConnectionProvider connections,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 25;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Outbox pass failed; will retry in {Interval}", PollInterval);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();

        var pending = await context.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        var connection = await connections.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await MessageTopology.DeclareExchangeAsync(channel, cancellationToken);

        foreach (var message in pending)
        {
            // The publish span joins the trace that raised the event, so the
            // asynchronous hop stays on the same end-to-end trace.
            using var activity = MessagingDiagnostics.ActivitySource.StartActivity(
                $"publish {message.Type}",
                ActivityKind.Producer,
                MessagingDiagnostics.ParseTraceParent(message.TraceParent));
            activity?.SetTag("messaging.destination.name", MessageTopology.Exchange);
            activity?.SetTag("messaging.message.id", message.Id.ToString());

            try
            {
                var headers = new Dictionary<string, object?>();
                var traceParent = activity?.Id ?? message.TraceParent;
                if (traceParent is not null)
                {
                    headers[MessagingDiagnostics.TraceParentHeader] = traceParent;
                }

                if (message.CorrelationId is not null)
                {
                    headers[MessagingDiagnostics.CorrelationIdHeader] = message.CorrelationId;
                }

                var properties = new BasicProperties
                {
                    MessageId = message.Id.ToString(),
                    Type = message.Type,
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    Headers = headers,
                };

                await channel.BasicPublishAsync(
                    MessageTopology.Exchange,
                    MessageTopology.RoutingKeyFor(message.Type),
                    mandatory: false,
                    basicProperties: properties,
                    body: Encoding.UTF8.GetBytes(message.Payload),
                    cancellationToken: cancellationToken);

                message.ProcessedAt = timeProvider.GetUtcNow();
                logger.LogInformation(
                    "Outbox message {MessageId} ({Type}) published to {Exchange}",
                    message.Id, message.Type, MessageTopology.Exchange);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = exception.Message;
                logger.LogWarning(
                    exception,
                    "Publishing outbox message {MessageId} failed (attempt {Attempts}); it stays pending",
                    message.Id, message.Attempts);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
