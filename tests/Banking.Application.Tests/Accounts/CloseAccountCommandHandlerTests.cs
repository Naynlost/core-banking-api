using Banking.Application.Accounts;
using Banking.Application.Accounts.CloseAccount;
using Banking.Application.Tests.Fakes;
using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Application.Tests.Accounts;

public class CloseAccountCommandHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryTransactionRepository _transactions = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Account _account;

    public CloseAccountCommandHandlerTests()
    {
        _account = Account.Open("user-1", Currency.Try).Value;
        _accounts.AddAsync(_account, CancellationToken.None);
    }

    private CloseAccountCommandHandler Handler => new(_accounts, _transactions, _unitOfWork);

    [Fact]
    public async Task Handle_WithZeroBalance_ClosesTheAccount()
    {
        var result = await Handler.HandleAsync(
            new CloseAccountCommand(_account.Id.Value, "user-1"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _account.IsClosed.ShouldBeTrue();
        _unitOfWork.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_WithRemainingBalance_FailsWithoutClosing()
    {
        _transactions.SetTotals(_account.Id, debits: 0, credits: 10m);

        var result = await Handler.HandleAsync(
            new CloseAccountCommand(_account.Id.Value, "user-1"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.BalanceMustBeZero);
        _account.IsClosed.ShouldBeFalse();
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_ForAnotherUsersAccount_ReturnsNotFound()
    {
        var result = await Handler.HandleAsync(
            new CloseAccountCommand(_account.Id.Value, "user-2"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountApplicationErrors.NotFound);
        _account.IsClosed.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_WhenAlreadyClosed_Fails()
    {
        _account.Close();

        var result = await Handler.HandleAsync(
            new CloseAccountCommand(_account.Id.Value, "user-1"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.AlreadyClosed);
    }
}
