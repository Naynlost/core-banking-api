using Banking.Application.Accounts.CreateAccount;
using Banking.Application.Tests.Fakes;
using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Application.Tests.Accounts;

public class CreateAccountCommandHandlerTests
{
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CreateAccountCommandHandler Handler => new(_accounts, _unitOfWork);

    [Fact]
    public async Task Handle_WithValidInput_PersistsAccountAndReturnsItsId()
    {
        var result = await Handler.HandleAsync(new CreateAccountCommand("user-1", "TRY"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var saved = _accounts.Accounts.ShouldHaveSingleItem();
        saved.Id.Value.ShouldBe(result.Value);
        saved.Owner.ShouldBe("user-1");
        saved.Currency.ShouldBe(Currency.Try);
        _unitOfWork.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_NormalizesLowercaseCurrencyCode()
    {
        var result = await Handler.HandleAsync(new CreateAccountCommand("user-1", " try "), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _accounts.Accounts.Single().Currency.ShouldBe(Currency.Try);
    }

    [Theory]
    [InlineData("")]
    [InlineData("TL")]
    [InlineData("TURKLIRASI")]
    public async Task Handle_WithInvalidCurrency_FailsWithoutPersisting(string code)
    {
        var result = await Handler.HandleAsync(new CreateAccountCommand("user-1", code), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CurrencyErrors.InvalidCode);
        _accounts.Accounts.ShouldBeEmpty();
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_WithBlankOwner_Fails()
    {
        var result = await Handler.HandleAsync(new CreateAccountCommand("  ", "TRY"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.OwnerRequired);
        _accounts.Accounts.ShouldBeEmpty();
    }
}
