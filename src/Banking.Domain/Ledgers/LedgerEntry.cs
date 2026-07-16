using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

/// <summary>
/// One immutable line in the ledger: an account gets debited or credited by a
/// positive amount as part of a transaction. Entries are never updated or
/// deleted; if something went wrong, post a reversal.
/// </summary>
public sealed record LedgerEntry
{
    // For EF materialization only; the data was validated when it was written.
    private LedgerEntry()
    {
        Amount = null!;
    }

    private LedgerEntry(
        Guid id,
        TransactionId transactionId,
        AccountId accountId,
        Money amount,
        EntryDirection direction,
        DateTimeOffset occurredAt)
    {
        Id = id;
        TransactionId = transactionId;
        AccountId = accountId;
        Amount = amount;
        Direction = direction;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; }

    public TransactionId TransactionId { get; }

    public AccountId AccountId { get; }

    /// <summary>Always positive; <see cref="Direction"/> carries the sign.</summary>
    public Money Amount { get; }

    public EntryDirection Direction { get; }

    public DateTimeOffset OccurredAt { get; }

    // Internal on purpose: entries are only created via Transaction.Create,
    // so an entry can't exist outside a balanced transaction.
    internal static LedgerEntry Create(
        TransactionId transactionId,
        AccountId accountId,
        Money amount,
        EntryDirection direction,
        DateTimeOffset occurredAt) =>
        new(Guid.NewGuid(), transactionId, accountId, amount, direction, occurredAt);
}
