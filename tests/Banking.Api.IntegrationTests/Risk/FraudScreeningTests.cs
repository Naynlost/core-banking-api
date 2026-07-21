using Banking.Domain.Fraud;
using Banking.Domain.Ledgers;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Banking.Api.IntegrationTests.Risk;

// Eşik üstü transfer outbox üzerinden fraud consumer'a ulaşır ve alert kaydeder
[Collection(IntegrationCollection.Name)]
public sealed class FraudScreeningTests(IntegrationInfrastructure infrastructure)
{
    [Fact]
    public async Task TransferAboveReviewThreshold_IsFlaggedByTheFraudConsumer()
    {
        await using var provider = await IntegrationTestServices.CreateProviderAsync(infrastructure);
        // 15.000: 10.000 eşiğinin üstünde ama 20.000 günlük limitin altında
        var source = await TestBank.CreateAccountAsync(provider, "user-a", fundedWith: 16_000m);
        var destination = await TestBank.CreateAccountAsync(provider, "user-b");

        var hostedServices = provider.GetServices<IHostedService>().ToList();
        foreach (var service in hostedServices)
        {
            await service.StartAsync(CancellationToken.None);
        }

        try
        {
            var result = await TestBank.TransferAsync(provider, source, destination, 15_000m);
            result.IsSuccess.ShouldBeTrue();
            var transactionId = new TransactionId(result.Value);

            await TestBank.WaitUntilAsync(
                async () =>
                {
                    await using var scope = provider.CreateAsyncScope();
                    var context = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
                    return await context.FraudAlerts.AnyAsync(a =>
                        a.TransactionId == transactionId && a.Rule == FraudPolicy.AmountAboveThresholdRule);
                },
                $"a fraud alert for transaction {transactionId}");

            // Transfer normal şekilde geçti; tarama işaretler, engellemez
            (await TestBank.GetBalanceAsync(provider, destination)).ShouldBe(15_000m);
        }
        finally
        {
            foreach (var service in hostedServices)
            {
                await service.StopAsync(CancellationToken.None);
            }
        }
    }
}
