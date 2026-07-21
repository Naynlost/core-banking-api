using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Banking.Api.IntegrationTests.EndToEnd;

// Gerçek API'yi tam pipeline'ıyla Testcontainers'a bağlar; UseSetting değerleri Program.cs'den ÖNCE besler
internal sealed class BankingApiFactory(
    IntegrationInfrastructure infrastructure,
    int authRateLimit = 1_000,
    IReadOnlyDictionary<string, string>? extraSettings = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var (key, value) in extraSettings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

        builder.UseSetting("ConnectionStrings:BankingDb", infrastructure.PostgresConnectionString);
        builder.UseSetting("RabbitMq:HostName", infrastructure.RabbitMqHost);
        builder.UseSetting("RabbitMq:Port", infrastructure.RabbitMqPort.ToString());
        builder.UseSetting("RabbitMq:UserName", IntegrationInfrastructure.RabbitMqUserName);
        builder.UseSetting("RabbitMq:Password", IntegrationInfrastructure.RabbitMqPassword);
        builder.UseSetting("Jwt:Secret", "e2e-test-secret-not-a-real-one-0123456789abcdef");
        // Tüm testler aynı loopback IP'yi paylaşır, test seçmediği sürece limit gevşetilir
        builder.UseSetting("RateLimiting:AuthPermitLimit", authRateLimit.ToString());
    }
}
