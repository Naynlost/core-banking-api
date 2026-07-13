using Banking.Domain.Primitives;

namespace Banking.Domain.ValueObjects;

/// <summary>
/// ISO 4217 currency code. Value object with structural equality.
/// </summary>
public sealed record Currency
{
    public static readonly Currency Try = new("TRY");
    public static readonly Currency Usd = new("USD");
    public static readonly Currency Eur = new("EUR");

    private Currency(string code) => Code = code;

    public string Code { get; }

    public static Result<Currency> Create(string code)
    {
        if (code.Length != 3 || !code.All(char.IsAsciiLetterUpper))
        {
            return Result.Failure<Currency>(CurrencyErrors.InvalidCode);
        }

        return Result.Success(new Currency(code));
    }

    public override string ToString() => Code;
}

public static class CurrencyErrors
{
    public const string InvalidCode = "currency.invalid_code";
}
