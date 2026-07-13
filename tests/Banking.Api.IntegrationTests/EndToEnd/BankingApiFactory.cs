using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Banking.Api.IntegrationTests.EndToEnd;

/// <summary>
/// Boots the real API — full pipeline, hosted services included (outbox
/// publisher and consumers run for real) — pointed at the Testcontainers
/// PostgreSQL and RabbitMQ instances. UseSetting feeds the values into the
/// configuration BEFORE Program.cs runs; ConfigureAppConfiguration would be
/// too late for values Program reads during startup.
/// </summary>
internal sealed class BankingApiFactory(IntegrationInfrastructure infrastructure) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:BankingDb", infrastructure.PostgresConnectionString);
        builder.UseSetting("RabbitMq:HostName", infrastructure.RabbitMqHost);
        builder.UseSetting("RabbitMq:Port", infrastructure.RabbitMqPort.ToString());
        builder.UseSetting("RabbitMq:UserName", IntegrationInfrastructure.RabbitMqUserName);
        builder.UseSetting("RabbitMq:Password", IntegrationInfrastructure.RabbitMqPassword);
        builder.UseSetting("Jwt:Secret", "e2e-test-secret-not-a-real-one-0123456789abcdef");
    }
}
