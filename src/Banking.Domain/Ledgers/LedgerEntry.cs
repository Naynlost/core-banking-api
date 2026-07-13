using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

/// <summary>
/// A single immutable line in the ledger: one account is debited or credited
/// by a positive amount as part of a transaction. Entries are never updated
/// or deleted; corrections are made by posting reversal entries.
/// </summary>
public sealed record LedgerEntry
{
    // Materialization-only constructor: filled by the persistence layer from
    // already-validated rows.
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

    /// <summary>Always positive; the sign is carried by <see cref="Direction"/>.</summary>
    public Money Amount { get; }

    public EntryDirection Direction { get; }

    public DateTimeOffset OccurredAt { get; }

    // Entries can only be created through Transaction.Create so that no entry
    // ever exists outside a balanced transaction.
    internal static LedgerEntry Create(
        TransactionId transactionId,
        AccountId accountId,
        Money amount,
        EntryDirection direction,
        DateTimeOffset occurredAt) =>
        new(Guid.NewGuid(), transactionId, accountId, amount, direction, occurredAt);
}
