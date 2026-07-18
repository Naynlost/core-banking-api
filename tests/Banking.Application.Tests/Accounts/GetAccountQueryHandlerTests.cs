using Banking.Application.Accounts;
using Banking.Application.Accounts.GetAccount;
using Banking.Application.Tests.Fakes;
using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Application.Tests.Accounts;

public class GetAccountQueryHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryBalanceProjection _balances = new();

    private GetAccountQueryHandler Handler => new(_accounts, _balances);

    private async Task<Account> SeedAccountAsync(string owner)
    {
        var account = Account.Open(owner, Currency.Try).Value;
        await _accounts.AddAsync(account, CancellationToken.None);
        return account;
    }

    [Fact]
    public async Task Handle_ForOwnAccount_ReturnsAccountWithDerivedBalance()
    {
        var account = await SeedAccountAsync("user-1");
        _balances.SetTotals(account.Id, debits: 40m, credits: 100m); // balance: 60 TRY

        var result = await Handler.HandleAsync(
            new GetAccountQuery(account.Id.Value, "user-1"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new AccountResponse(
            account.Id.Value, "TRY", "Liability", "Active", "Pending", Account.DefaultDailyTransferLimit, 60m));
    }

    [Fact]
    public async Task Handle_ForUnknownAccount_ReturnsNotFound()
    {
        var result = await Handler.HandleAsync(
            new GetAccountQuery(Guid.NewGuid(), "user-1"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountApplicationErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ForAnotherUsersAccount_ReturnsNotFoundToHideExistence()
    {
        var account = await SeedAccountAsync("user-1");

        var result = await Handler.HandleAsync(
            new GetAccountQuery(account.Id.Value, "user-2"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountApplicationErrors.NotFound);
    }
}
