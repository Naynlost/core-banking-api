using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Banking.Api.IntegrationTests;

// Testcontainers ile tüm çalıştırma boyunca tek Postgres + RabbitMQ; docker-compose dev verisine dokunmaz
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

// Tüm integration testleri aynı koleksiyonda; aynı kuyrukları paylaştıklarından paralelde çakışırlardı
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationInfrastructure>
{
    public const string Name = "integration";
}
