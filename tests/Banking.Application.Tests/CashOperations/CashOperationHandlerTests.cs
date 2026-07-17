using Banking.Application.Abstractions;
using Banking.Application.Accounts;
using Banking.Application.CashOperations;
using Banking.Application.Tests.Fakes;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Application.Tests.CashOperations;

public class CashOperationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryTransactionRepository _transactions = new();
    private readonly StagingIdempotencyStore _idempotency = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly Account _account;
    private readonly IServiceScopeFactory _scopeFactory;

    public CashOperationHandlerTests()
    {
        _unitOfWork = new FakeUnitOfWork(_idempotency);
        _account = Account.Open("user-1", Currency.Try).Value;
        _accounts.AddAsync(_account, CancellationToken.None);

        var services = new ServiceCollection();
        services.AddSingleton<IAccountRepository>(_accounts);
        services.AddSingleton<ITransactionRepository>(_transactions);
        services.AddSingleton<IIdempotencyStore>(_idempotency);
        services.AddSingleton<IUnitOfWork>(_unitOfWork);
        _scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private DepositMoneyCommandHandler DepositHandler => new(_scopeFactory, new FixedTimeProvider(Now));

    private WithdrawMoneyCommandHandler WithdrawHandler => new(_scopeFactory, new FixedTimeProvider(Now));

    [Fact]
    public async Task Deposit_CreatesCashAccountAndPostsBalancedTransaction()
    {
        var result = await DepositHandler.HandleAsync(
            new DepositMoneyCommand("key-1", "user-1", _account.Id.Value, 100m, "TRY"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var cash = _accounts.Accounts.Single(a => a.Owner == Account.SystemOwner);
        cash.Type.ShouldBe(AccountType.Asset);

        var transaction = _transactions.Added.ShouldHaveSingleItem();
        transaction.Description.ShouldBe(CashPolicy.DepositDescription);
        transaction.Entries.ShouldContain(e => e.AccountId == cash.Id && e.Direction == EntryDirection.Debit);
        transaction.Entries.ShouldContain(e => e.AccountId == _account.Id && e.Direction == EntryDirection.Credit);

        _idempotency.Committed.ShouldHaveSingleItem().TransactionId.ShouldBe(result.Value);
        _account.Version.ShouldBe(1);
        cash.Version.ShouldBe(1);
    }

    [Fact]
    public async Task Deposit_ReusesTheExistingCashAccount()
    {
        var cash = Account.OpenCash(Currency.Try);
        await _accounts.AddAsync(cash, CancellationToken.None);

        var result = await DepositHandler.HandleAsync(
            new DepositMoneyCommand("key-1", "user-1", _account.Id.Value, 100m, "TRY"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _accounts.Accounts.Count(a => a.Owner == Account.SystemOwner).ShouldBe(1);
    }

    [Fact]
    public async Task Deposit_WithKnownIdempotencyKey_ReturnsStoredResultWithoutExecuting()
    {
        var storedTransactionId = Guid.NewGuid();
        _idempotency.SeedCommitted(new IdempotencyRecord("key-1", "user-1", storedTransactionId, Now));

        var result = await DepositHandler.HandleAsync(
            new DepositMoneyCommand("key-1", "user-1", _account.Id.Value, 100m, "TRY"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(storedTransactionId);
        _transactions.Added.ShouldBeEmpty();
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Deposit_OnAnotherUsersAccount_ReturnsNotFound()
    {
        var result = await DepositHandler.HandleAsync(
            new DepositMoneyCommand("key-1", "user-2", _account.Id.Value, 100m, "TRY"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountApplicationErrors.NotFound);
        _transactions.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Withdraw_WithSufficientBalance_PostsBalancedTransaction()
    {
        _transactions.SetTotals(_account.Id, debits: 0, credits: 100m); // balance: 100 TRY

        var result = await WithdrawHandler.HandleAsync(
            new WithdrawMoneyCommand("key-1", "user-1", _account.Id.Value, 60m, "TRY"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var transaction = _transactions.Added.ShouldHaveSingleItem();
        transaction.Description.ShouldBe(CashPolicy.WithdrawalDescription);
        transaction.Entries.ShouldContain(e => e.AccountId == _account.Id && e.Direction == EntryDirection.Debit);
        _unitOfWork.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Withdraw_WithInsufficientBalance_FailsWithoutSaving()
    {
        _transactions.SetTotals(_account.Id, debits: 0, credits: 50m);

        var result = await WithdrawHandler.HandleAsync(
            new WithdrawMoneyCommand("key-1", "user-1", _account.Id.Value, 50.01m, "TRY"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.InsufficientFunds);
        _transactions.Added.ShouldBeEmpty();
        _unitOfWork.SaveCount.ShouldBe(0);
    }
}
