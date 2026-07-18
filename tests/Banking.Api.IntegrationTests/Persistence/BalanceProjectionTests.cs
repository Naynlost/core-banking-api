using Banking.Application.Abstractions;
using Banking.Application.Accounts.GetAccount;
using Banking.Application.Messaging;
using Banking.Application.Transactions.ReverseTransaction;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Api.IntegrationTests.Persistence;

/// <summary>
/// The account_balances read model against real PostgreSQL: it is written in
/// the same transaction as every ledger write, so after any mix of operations
/// it must agree exactly with totals summed from the ledger itself — and the
/// account queries read the projection.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class BalanceProjectionTests(IntegrationInfrastructure infrastructure)
{
    [Fact]
    public async Task Projection_MatchesLedgerTotals_AfterDepositTransferAndReversal()
    {
        await using var provider = await IntegrationTestServices.CreateProviderAsync(infrastructure);
        var source = await TestBank.CreateAccountAsync(provider, "proj-user-a", fundedWith: 1_000m);
        var destination = await TestBank.CreateAccountAsync(provider, "proj-user-b");

        var transfer = await TestBank.TransferAsync(provider, source, destination, 400m);
        transfer.IsSuccess.ShouldBeTrue();

        await using (var scope = provider.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            var reversal = await dispatcher.SendAsync(
                new ReverseTransactionCommand(transfer.Value, "proj-user-b"), CancellationToken.None);
            reversal.IsSuccess.ShouldBeTrue(reversal.IsFailure ? reversal.Error : string.Empty);
        }

        // Projection totals equal the authoritative SUM over ledger_entries.
        await using (var scope = provider.CreateAsyncScope())
        {
            var projection = scope.ServiceProvider.GetRequiredService<IBalanceProjection>();
            var ledger = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

            foreach (var account in new[] { source, destination })
            {
                var projected = await projection.GetTotalsAsync(account.Id, CancellationToken.None);
                var summed = await ledger.GetEntryTotalsAsync(account.Id, CancellationToken.None);
                projected.ShouldBe(summed);
            }
        }

        // The account query serves the projected balance: back to the funded 1.000.
        await using (var scope = provider.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            var response = await dispatcher.QueryAsync(
                new GetAccountQuery(source.Id.Value, "proj-user-a"), CancellationToken.None);
            response.IsSuccess.ShouldBeTrue();
            response.Value.Balance.ShouldBe(1_000m);
        }
    }

    [Fact]
    public async Task Projection_ForAnAccountWithoutMovements_ReadsZero()
    {
        await using var provider = await IntegrationTestServices.CreateProviderAsync(infrastructure);
        var account = await TestBank.CreateAccountAsync(provider, "proj-user-c");

        await using var scope = provider.CreateAsyncScope();
        var projection = scope.ServiceProvider.GetRequiredService<IBalanceProjection>();

        (await projection.GetTotalsAsync(account.Id, CancellationToken.None))
            .ShouldBe(new EntryTotals(0m, 0m));
    }
}
