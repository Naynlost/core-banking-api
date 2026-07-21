using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Domain.Tests.ValueObjects;

public class ExchangeRateTests
{
    private static ExchangeRate Rate(decimal rate, Currency? from = null, Currency? to = null) =>
        ExchangeRate.Create(from ?? Currency.Try, to ?? Currency.Usd, rate).Value;

    private static Money Try(decimal amount) => Money.Create(amount, Currency.Try).Value;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveRate_Fails(decimal rate)
    {
        var result = ExchangeRate.Create(Currency.Try, Currency.Usd, rate);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ExchangeRateErrors.RateMustBePositive);
    }

    [Fact]
    public void Convert_ProducesTheTargetCurrency()
    {
        var converted = Rate(0.024m).Convert(Try(1_000m));

        converted.IsSuccess.ShouldBeTrue();
        converted.Value.Currency.ShouldBe(Currency.Usd);
        converted.Value.Amount.ShouldBe(24m);
    }

    [Fact]
    public void Convert_RoundsToTwoDecimals()
    {
        // 100 × 0.12345 = 12.345 → defterdeki numeric(18,2) ölçeğine iner
        var converted = Rate(0.12345m).Convert(Try(100m));

        converted.Value.Amount.ShouldBe(12.34m);
    }

    [Fact]
    public void Convert_UsesBankersRounding()
    {
        // Tam yarım değerler sistematik olarak yukarı değil, en yakın çifte gider
        Rate(0.125m).Convert(Try(100m)).Value.Amount.ShouldBe(12.50m);
        Rate(1.005m).Convert(Try(1m)).Value.Amount.ShouldBe(1.00m);
        Rate(1.015m).Convert(Try(1m)).Value.Amount.ShouldBe(1.02m);
    }

    [Fact]
    public void Convert_WithWrongSourceCurrency_Fails()
    {
        var converted = Rate(0.024m).Convert(Money.Create(10m, Currency.Eur).Value);

        converted.IsFailure.ShouldBeTrue();
        converted.Error.ShouldBe(ExchangeRateErrors.CurrencyMismatch);
    }

    [Fact]
    public void Convert_WhenResultRoundsToZero_Fails()
    {
        // 0.01 TRY çok küçük bir kurla çevrilince 0.00 çıkar; sıfır tutarlı defter
        // satırı yazmak yerine anlaşılır bir hata döner
        var converted = Rate(0.0001m).Convert(Try(0.01m));

        converted.IsFailure.ShouldBeTrue();
        converted.Error.ShouldBe(ExchangeRateErrors.AmountTooSmall);
    }
}
