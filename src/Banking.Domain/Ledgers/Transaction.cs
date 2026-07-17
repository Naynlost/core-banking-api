using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

/// <summary>
/// One leg of a transaction before it becomes an immutable ledger entry.
/// </summary>
public readonly record struct EntrySpec(AccountId AccountId, Money Amount, EntryDirection Direction);

/// <summary>
/// A financial transaction: at least two ledger entries in the same currency,
/// with total debits equal to total credits (the double-entry invariant).
/// </summary>
public sealed class Transaction
{
    private readonly List<LedgerEntry> _entries;

    // For EF materialization only; the data was validated when it was written.
    private Transaction()
    {
        _entries = [];
        Description = null!;
    }

    private Transaction(
        TransactionId id,
        string description,
        DateTimeOffset occurredAt,
        List<LedgerEntry> entries,
        TransactionId? reverses)
    {
        Id = id;
        Description = description;
        OccurredAt = occurredAt;
        _entries = entries;
        ReversesTransactionId = reverses;
    }

    public TransactionId Id { get; }

    public string Description { get; }

    public DateTimeOffset OccurredAt { get; }

    /// <summary>Set when this transaction is the reversal of another one.</summary>
    public TransactionId? ReversesTransactionId { get; }

    public IReadOnlyList<LedgerEntry> Entries => _entries;

    public static Result<Transaction> Create(
        string description,
        DateTimeOffset occurredAt,
        IReadOnlyCollection<EntrySpec> entries,
        TransactionId? reverses = null)
    {
        if (entries.Count < 2)
        {
            return Result.Failure<Transaction>(TransactionErrors.AtLeastTwoEntries);
        }

        var currency = entries.First().Amount.Currency;
        decimal totalDebit = 0;
        decimal totalCredit = 0;

        foreach (var entry in entries)
        {
            if (entry.Amount.IsZero)
            {
                return Result.Failure<Transaction>(TransactionErrors.EntryAmountMustBePositive);
            }

            if (entry.Amount.Currency != currency)
            {
                return Result.Failure<Transaction>(TransactionErrors.MixedCurrencies);
            }

            if (entry.Direction == EntryDirection.Debit)
            {
                totalDebit += entry.Amount.Amount;
            }
            else
            {
                totalCredit += entry.Amount.Amount;
            }
        }

        if (totalDebit != totalCredit)
        {
            return Result.Failure<Transaction>(TransactionErrors.Unbalanced);
        }

        var id = TransactionId.New();
        var ledgerEntries = entries
            .Select(e => LedgerEntry.Create(id, e.AccountId, e.Amount, e.Direction, occurredAt))
            .ToList();

        return Result.Success(new Transaction(id, description, occurredAt, ledgerEntries, reverses));
    }
}

public static class TransactionErrors
{
    public const string AtLeastTwoEntries = "transaction.at_least_two_entries";
    public const string EntryAmountMustBePositive = "transaction.entry_amount_must_be_positive";
    public const string MixedCurrencies = "transaction.mixed_currencies";
    public const string Unbalanced = "transaction.unbalanced";
}
