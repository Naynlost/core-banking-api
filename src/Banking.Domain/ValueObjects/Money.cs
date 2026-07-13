using Banking.Domain.Primitives;

namespace Banking.Domain.ValueObjects;

/// <summary>
/// An amount of money in a single currency. Amounts are always non-negative;
/// direction (debit/credit) is expressed on the ledger entry, not on the money itself.
/// </summary>
public sealed record Money
{
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public bool IsZero => Amount == 0;

    public static Result<Money> Create(decimal amount, Currency currency)
    {
        if (amount < 0)
        {
            return Result.Failure<Money>(MoneyErrors.NegativeAmount);
        }

        return Result.Success(new Money(amount, currency));
    }

    public static Money Zero(Currency currency) => new(0, currency);

    public Result<Money> Add(Money other)
    {
        if (Currency != other.Currency)
        {
            return Result.Failure<Money>(MoneyErrors.CurrencyMismatch);
        }

        return Result.Success(new Money(Amount + other.Amount, Currency));
    }

    public Result<Money> Subtract(Money other)
    {
        if (Currency != other.Currency)
        {
            return Result.Failure<Money>(MoneyErrors.CurrencyMismatch);
        }

        if (other.Amount > Amount)
        {
            return Result.Failure<Money>(MoneyErrors.NegativeResult);
        }

        return Result.Success(new Money(Amount - other.Amount, Currency));
    }

    public override string ToString() => $"{Amount} {Currency}";
}

public static class MoneyErrors
{
    public const string NegativeAmount = "money.negative_amount";
    public const string NegativeResult = "money.negative_result";
    public const string CurrencyMismatch = "money.currency_mismatch";
}
