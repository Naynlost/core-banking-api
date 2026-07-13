namespace Banking.Domain.Ledgers;

public readonly record struct TransactionId(Guid Value)
{
    public static TransactionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
