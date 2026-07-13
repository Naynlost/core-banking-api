using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Banking.Api.IntegrationTests;

/// <summary>
/// One PostgreSQL and one RabbitMQ container for the whole integration test
/// run (Testcontainers): the suite needs nothing pre-installed besides Docker,
/// locally and in CI alike, and never touches the docker-compose dev data.
/// </summary>
public sealed class IntegrationInfrastructure : IAsyncLifetime
{
    public const string RabbitMqUserName = "banking";
    public const string RabbitMqPassword = "banking_tests";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-alpine")
        .WithUsername(RabbitMqUserName)
        .WithPassword(RabbitMqPassword)
        .Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();

    public string RabbitMqHost => _rabbitMq.Hostname;

    public int RabbitMqPort => _rabbitMq.GetMappedPublicPort(5672);

    public async Task InitializeAsync() =>
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}

/// <summary>
/// All integration test classes join this collection: they share the two
/// containers and run sequentially, because they exchange messages over the
/// same broker queues and would steal each other's deliveries in parallel.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationInfrastructure>
{
    public const string Name = "integration";
}
