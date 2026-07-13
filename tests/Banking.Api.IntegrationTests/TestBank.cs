using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Application.Transfers;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Api.IntegrationTests;

/// <summary>Shared account and transfer plumbing for integration tests.</summary>
internal static class TestBank
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Opens an account (KYC verified by default so it can send transfers).
    /// Funding happens like a real deposit: a balanced transaction against a
    /// cash account.
    /// </summary>
    public static async Task<Account> CreateAccountAsync(
        IServiceProvider provider, string owner, decimal fundedWith = 0m, bool kycVerified = true)
    {
        await using var scope = provider.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var transactions = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var account = Account.Open(owner, Currency.Try).Value;
        if (kycVerified)
        {
            account.CompleteKyc();
        }

        await accounts.AddAsync(account, CancellationToken.None);

        if (fundedWith > 0m)
        {
            var cash = Account.OpenCash(Currency.Try);
            await accounts.AddAsync(cash, CancellationToken.None);

            var amount = Money.Create(fundedWith, Currency.Try).Value;
            var deposit = Transaction.Create(
                "Deposit",
                DateTimeOffset.UtcNow,
                [
                    new EntrySpec(cash.Id, amount, EntryDirection.Debit),
                    new EntrySpec(account.Id, amount, EntryDirection.Credit),
                ]).Value;

            cash.RecordMovement();
            account.RecordMovement();
            await transactions.AddAsync(deposit, CancellationToken.None);
        }

        await unitOfWork.SaveChangesAsync(CancellationToken.None);
        return account;
    }

    /// <summary>Transfers with a fresh idempotency key; use SendAsync for replay scenarios.</summary>
    public static async Task<Result<Guid>> TransferAsync(
        IServiceProvider provider, Account source, Account destination, decimal amount) =>
        await SendAsync(provider, new TransferMoneyCommand(
            $"key-{Guid.NewGuid()}", source.Owner, source.Id.Value, destination.Id.Value, amount, "TRY"));

    public static async Task<Result<Guid>> SendAsync(IServiceProvider provider, TransferMoneyCommand command)
    {
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        return await dispatcher.SendAsync(command, CancellationToken.None);
    }

    public static async Task<decimal> GetBalanceAsync(IServiceProvider provider, Account account)
    {
        await using var scope = provider.CreateAsyncScope();
        var transactions = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var totals = await transactions.GetEntryTotalsAsync(account.Id, CancellationToken.None);
        return LedgerMath.Balance(account, totals.Debits, totals.Credits).Amount;
    }

    /// <summary>Polls until the condition holds; throws after 60 seconds.</summary>
    public static async Task WaitUntilAsync(Func<Task<bool>> condition, string description)
    {
        var deadline = DateTimeOffset.UtcNow + WaitTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException($"Timed out after {WaitTimeout} waiting for {description}.");
    }
}
