using Banking.Application;
using Banking.Infrastructure;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Api.IntegrationTests;

// Testcontainers Postgres/RabbitMQ'ya karşı gerçek servis grafiğini kurar, migration'ları uygular
internal static class IntegrationTestServices
{
    public static async Task<ServiceProvider> CreateProviderAsync(IntegrationInfrastructure infrastructure)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:BankingDb"] = infrastructure.PostgresConnectionString,
                ["Jwt:Issuer"] = "Banking.Api.Tests",
                ["Jwt:Audience"] = "Banking.Api.Tests",
                ["Jwt:Secret"] = "integration-test-secret-not-a-real-one-0123456789",
                ["RabbitMq:HostName"] = infrastructure.RabbitMqHost,
                ["RabbitMq:Port"] = infrastructure.RabbitMqPort.ToString(),
                ["RabbitMq:UserName"] = IntegrationInfrastructure.RabbitMqUserName,
                ["RabbitMq:Password"] = IntegrationInfrastructure.RabbitMqPassword,

                // Testlerde okunabilir sayılar için yuvarlak kur: 1 USD = 40 TRY,
                // yani 1.000 TRY tam 25 USD eder.
                ["Fx:BaseCurrency"] = "TRY",
                ["Fx:Rates:USD"] = "40",
                ["Fx:Rates:EUR"] = "50",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
            await context.Database.MigrateAsync();
        }

        return provider;
    }
}
