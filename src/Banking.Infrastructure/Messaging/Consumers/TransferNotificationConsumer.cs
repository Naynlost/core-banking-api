using System.Text.Json;
using Banking.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Banking.Infrastructure.Messaging.Consumers;

// Gerçek bildirim kanalının (e-posta/SMS/push) yerine geçer, sadece loglar
internal sealed class TransferNotificationConsumer(
    RabbitMqConnectionProvider connections,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<TransferNotificationConsumer> logger)
    : RabbitMqEventConsumer(connections, scopeFactory, timeProvider, logger)
{
    private readonly ILogger<TransferNotificationConsumer> _logger = logger;

    protected override string ConsumerName => MessageTopology.NotificationsQueue;

    protected override string RoutingKey => MessageTopology.MoneyTransferredRoutingKey;

    protected override Task HandleAsync(string eventType, string payload, CancellationToken cancellationToken)
    {
        var transfer = JsonSerializer.Deserialize<MoneyTransferred>(payload)
            ?? throw new JsonException($"Empty {eventType} payload.");

        _logger.LogInformation(
            "Notification: {Amount} {Currency} transferred from account {SourceAccountId} "
            + "to account {DestinationAccountId} (transaction {TransactionId})",
            transfer.Amount,
            transfer.CurrencyCode,
            transfer.SourceAccountId,
            transfer.DestinationAccountId,
            transfer.TransactionId);

        return Task.CompletedTask;
    }
}
