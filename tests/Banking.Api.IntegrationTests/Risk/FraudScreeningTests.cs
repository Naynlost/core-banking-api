using Banking.Domain.Fraud;
using Banking.Domain.Ledgers;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Banking.Api.IntegrationTests.Risk;

/// <summary>
/// End-to-end fraud screening against real PostgreSQL and RabbitMQ: a transfer
/// above the review threshold goes through the outbox to the fraud consumer,
/// which persists a fraud alert for the transaction.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class FraudScreeningTests(IntegrationInfrastructure infrastructure)
{
    [Fact]
    public async Task TransferAboveReviewThreshold_IsFlaggedByTheFraudConsumer()
    {
        await using var provider = await IntegrationTestServices.CreateProviderAsync(infrastructure);
        // 15.000: above the 10.000 review threshold, still within the 20.000 daily limit.
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

            // The transfer itself went through — screening flags, it does not block.
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
