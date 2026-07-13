using Banking.Application.Abstractions;
using Banking.Application.Transfers;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Api.IntegrationTests.Transfers;

[Collection(IntegrationCollection.Name)]
public sealed class TransferMoneyTests(IntegrationInfrastructure infrastructure) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync() =>
        _provider = await IntegrationTestServices.CreateProviderAsync(infrastructure);

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task SameIdempotencyKey_AppliedTwice_ProducesExactlyOneTransfer()
    {
        var source = await TestBank.CreateAccountAsync(_provider, "user-a", fundedWith: 100m);
        var destination = await TestBank.CreateAccountAsync(_provider, "user-b");
        var command = new TransferMoneyCommand(
            $"key-{Guid.NewGuid()}", "user-a", source.Id.Value, destination.Id.Value, 40m, "TRY");

        var first = await TestBank.SendAsync(_provider, command);
        var second = await TestBank.SendAsync(_provider, command);

        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        second.Value.ShouldBe(first.Value); // the stored outcome is replayed

        (await TestBank.GetBalanceAsync(_provider, source)).ShouldBe(60m); // debited once, not twice
        (await TestBank.GetBalanceAsync(_provider, destination)).ShouldBe(40m);
        (await CountTransfersAsync(source)).ShouldBe(1);
    }

    [Fact]
    public async Task ParallelTransfers_NeverOverdrawTheSourceAccount()
    {
        const decimal initialBalance = 100m;
        const decimal transferAmount = 10m;
        const int attempts = 20; // twice the funds: at most 10 can succeed

        var source = await TestBank.CreateAccountAsync(_provider, "user-a", fundedWith: initialBalance);
        var destination = await TestBank.CreateAccountAsync(_provider, "user-b");

        var results = await Task.WhenAll(Enumerable.Range(0, attempts).Select(i => TestBank.SendAsync(
            _provider,
            new TransferMoneyCommand(
                $"key-{Guid.NewGuid()}", "user-a", source.Id.Value, destination.Id.Value, transferAmount, "TRY"))));

        var successes = results.Count(r => r.IsSuccess);
        results.Where(r => r.IsFailure).ShouldAllBe(r =>
            r.Error == TransferErrors.Conflict || r.Error == LedgerErrors.InsufficientFunds);

        successes.ShouldBeGreaterThan(0);
        successes.ShouldBeLessThanOrEqualTo((int)(initialBalance / transferAmount));

        var sourceBalance = await TestBank.GetBalanceAsync(_provider, source);
        var destinationBalance = await TestBank.GetBalanceAsync(_provider, destination);

        // The whole point: no lost update, no overdraft, money conserved.
        sourceBalance.ShouldBe(initialBalance - (transferAmount * successes));
        sourceBalance.ShouldBeGreaterThanOrEqualTo(0m);
        destinationBalance.ShouldBe(transferAmount * successes);
        (await CountTransfersAsync(source)).ShouldBe(successes);
    }

    private async Task<int> CountTransfersAsync(Account account)
    {
        await using var scope = _provider.CreateAsyncScope();
        var transactions = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var all = await transactions.GetByAccountIdAsync(account.Id, CancellationToken.None);
        return all.Count(t => t.Description == TransferPolicy.TransferDescription);
    }
}
