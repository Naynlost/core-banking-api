using RabbitMQ.Client;

namespace Banking.Infrastructure.Messaging;

/// <summary>
/// Single place that names the broker topology. Declarations are idempotent,
/// so publisher and consumers can each declare what they use and survive
/// starting in any order. Every consumer queue dead-letters into a shared
/// dead-letter queue, so a poison message is parked for inspection instead of
/// being lost or looping forever.
/// </summary>
internal static class MessageTopology
{
    public const string Exchange = "banking.events";

    public const string DeadLetterExchange = "banking.dlx";

    /// <summary>All dead letters land here; the original queue name is the routing key.</summary>
    public const string DeadLetterQueue = "banking.dead-letters";

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
        await DeclareDeadLetteringAsync(channel, cancellationToken);

        await channel.QueueDeclareAsync(
            queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange,
                ["x-dead-letter-routing-key"] = queue,
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue, Exchange, routingKey, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, queue, cancellationToken: cancellationToken);
    }

    private static async Task DeclareDeadLetteringAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
    }
}
