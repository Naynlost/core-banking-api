namespace Banking.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";

    public int Port { get; init; } = 5672;

    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string VirtualHost { get; init; } = "/";

    /// <summary>AMQPS (TLS) — required by managed brokers like CloudAMQP; off for local Docker.</summary>
    public bool UseTls { get; init; }
}
