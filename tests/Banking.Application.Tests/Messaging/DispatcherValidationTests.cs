using Banking.Application.Accounts.GetStatement;
using Banking.Application.Messaging;
using Banking.Application.Transfers;
using Banking.Domain.Ledgers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Application.Tests.Messaging;

// Repository/UnitOfWork kayıtlı değil; başarısız Result dönmesi mesajın handler'a hiç ulaşmadığını kanıtlar
public class DispatcherValidationTests
{
    private readonly IDispatcher _dispatcher;

    public DispatcherValidationTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        _dispatcher = services.BuildServiceProvider().GetRequiredService<IDispatcher>();
    }

    private static TransferMoneyCommand Transfer(
        string key = "key-1", decimal amount = 10m, Guid? destination = null) =>
        new(key, "user-1", Guid.NewGuid(), destination ?? Guid.NewGuid(), amount, "TRY");

    [Fact]
    public async Task Send_WithMissingIdempotencyKey_FailsBeforeTheHandler()
    {
        var result = await _dispatcher.SendAsync(Transfer(key: ""), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("transfer.idempotency_key_required");
    }

    [Fact]
    public async Task Send_WithZeroAmount_FailsWithTheDomainErrorCode()
    {
        var result = await _dispatcher.SendAsync(Transfer(amount: 0m), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.AmountMustBePositive);
    }

    [Fact]
    public async Task Send_ToTheSameAccount_FailsWithTheDomainErrorCode()
    {
        var accountId = Guid.NewGuid();
        var command = new TransferMoneyCommand("key-1", "user-1", accountId, accountId, 10m, "TRY");

        var result = await _dispatcher.SendAsync(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.SameAccount);
    }

    [Fact]
    public async Task Query_WithOutOfRangePageSize_Fails()
    {
        var result = await _dispatcher.QueryAsync(
            new GetAccountStatementQuery(Guid.NewGuid(), "user-1", Page: 1, PageSize: 0), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(StatementErrors.PageSizeOutOfRange);
    }
}
