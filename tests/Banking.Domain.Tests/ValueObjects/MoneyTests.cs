using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Create_WithNegativeAmount_Fails()
    {
        var result = Money.Create(-1m, Currency.Try);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MoneyErrors.NegativeAmount);
    }

    [Fact]
    public void Create_WithNonNegativeAmount_Succeeds()
    {
        var result = Money.Create(100.50m, Currency.Try);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(100.50m);
        result.Value.Currency.ShouldBe(Currency.Try);
    }

    [Fact]
    public void Zero_HasZeroAmount()
    {
        var zero = Money.Zero(Currency.Try);

        zero.IsZero.ShouldBeTrue();
        zero.Amount.ShouldBe(0m);
    }

    [Fact]
    public void Add_WithSameCurrency_SumsAmounts()
    {
        var result = Money.Create(100m, Currency.Try).Value
            .Add(Money.Create(25.25m, Currency.Try).Value);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(125.25m);
    }

    [Fact]
    public void Add_WithDifferentCurrency_Fails()
    {
        var result = Money.Create(100m, Currency.Try).Value
            .Add(Money.Create(100m, Currency.Usd).Value);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MoneyErrors.CurrencyMismatch);
    }

    [Fact]
    public void Subtract_WithSameCurrency_ReducesAmount()
    {
        var result = Money.Create(100m, Currency.Try).Value
            .Subtract(Money.Create(30m, Currency.Try).Value);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(70m);
    }

    [Fact]
    public void Subtract_WithDifferentCurrency_Fails()
    {
        var result = Money.Create(100m, Currency.Try).Value
            .Subtract(Money.Create(30m, Currency.Usd).Value);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MoneyErrors.CurrencyMismatch);
    }

    [Fact]
    public void Subtract_ThatWouldGoNegative_Fails()
    {
        var result = Money.Create(30m, Currency.Try).Value
            .Subtract(Money.Create(100m, Currency.Try).Value);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MoneyErrors.NegativeResult);
    }

    [Fact]
    public void Monies_WithSameAmountAndCurrency_AreEqual()
    {
        Money.Create(100m, Currency.Try).Value.ShouldBe(Money.Create(100m, Currency.Try).Value);
    }
}
