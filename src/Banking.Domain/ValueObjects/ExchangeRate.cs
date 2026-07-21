using Banking.Domain.Primitives;

namespace Banking.Domain.ValueObjects;

// İki para birimi arasındaki kur: 1 birim From = Rate birim To.
// Çevrim ve yuvarlama burada toplanır ki defterin her yerinde aynı sonucu versin.
public sealed record ExchangeRate
{
    // Defterdeki tutar kolonu numeric(18,2); çevrim sonucu da bu ölçeğe yuvarlanmalı.
    private const int MoneyScale = 2;

    private ExchangeRate(Currency from, Currency to, decimal rate)
    {
        From = from;
        To = to;
        Rate = rate;
    }

    public Currency From { get; }

    public Currency To { get; }

    public decimal Rate { get; }

    public static Result<ExchangeRate> Create(Currency from, Currency to, decimal rate)
    {
        if (rate <= 0)
        {
            return Result.Failure<ExchangeRate>(ExchangeRateErrors.RateMustBePositive);
        }

        return Result.Success(new ExchangeRate(from, to, rate));
    }

    public Result<Money> Convert(Money amount)
    {
        if (amount.Currency != From)
        {
            return Result.Failure<Money>(ExchangeRateErrors.CurrencyMismatch);
        }

        // Bankacılık yuvarlaması (yarıyı çifte): sistematik olarak bir tarafa kaymaz.
        var converted = Math.Round(amount.Amount * Rate, MoneyScale, MidpointRounding.ToEven);

        // Kur çok küçükse tutar sıfıra yuvarlanabilir; sıfır tutarlı bir defter satırı
        // yazmak yerine bunu anlaşılır bir hatayla reddediyoruz.
        if (converted == 0)
        {
            return Result.Failure<Money>(ExchangeRateErrors.AmountTooSmall);
        }

        return Money.Create(converted, To);
    }
}

public static class ExchangeRateErrors
{
    public const string RateMustBePositive = "fx.rate_must_be_positive";
    public const string CurrencyMismatch = "fx.currency_mismatch";
    public const string AmountTooSmall = "fx.amount_too_small";
    public const string RateNotAvailable = "fx.rate_not_available";
}
