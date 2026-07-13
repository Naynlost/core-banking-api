using RabbitMQ.Client;

namespace Banking.Infrastructure.Messaging;

/// <summary>
/// Single place that names the broker topology. Declarations are idempotent,
/// so publisher and consumers can each declare what they use and survive
/// starting in any order.
/// </summary>
internal static class MessageTopology
{
    public const string Exchange = "banking.events";

    public const string MoneyTransferredRoutingKey = "money.transferred";

    public const string NotificationsQueue = "notifications.money-transferred";

    public const string FraudQueue = "fraud.money-transferred";

    public static string RoutingKeyFor(string eventType) => eventType switch
    {
        "MoneyTransferred" => MoneyTransferredRoutingKey,
        _ => eventType.ToLowerInvariant(),
    };

    public static Task DeclareExchangeAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.ExchangeDeclareAsync(
            Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);

    public static async Task DeclareQueueAsync(
        IChannel channel, string queue, string routingKey, CancellationToken cancellationToken)
    {
        await DeclareExchangeAsync(channel, cancellationToken);
        await channel.QueueDeclareAsync(
            queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue, Exchange, routingKey, cancellationToken: cancellationToken);
    }
}
