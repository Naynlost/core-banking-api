using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

/// <summary>
/// Describes one leg of a transaction before it is turned into an immutable ledger entry.
/// </summary>
public readonly record struct EntrySpec(AccountId AccountId, Money Amount, EntryDirection Direction);

/// <summary>
/// A financial transaction: a set of at least two ledger entries in a single
/// currency where total debits equal total credits (double-entry invariant).
/// </summary>
public sealed class Transaction
{
    private readonly List<LedgerEntry> _entries;

    // Materialization-only constructor: object stays invariant-free until the
    // persistence layer fills it from already-validated rows.
    private Transaction()
    {
        _entries = [];
        Description = null!;
    }

    private Transaction(TransactionId id, string description, DateTimeOffset occurredAt, List<LedgerEntry> entries)
    {
        Id = id;
        Description = description;
        OccurredAt = occurredAt;
        _entries = entries;
    }

    public TransactionId Id { get; }

    public string Description { get; }

    public DateTimeOffset OccurredAt { get; }

    public IReadOnlyList<LedgerEntry> Entries => _entries;

    public static Result<Transaction> Create(
        string description,
        DateTimeOffset occurredAt,
        IReadOnlyCollection<EntrySpec> entries)
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

        return Result.Success(new Transaction(id, description, occurredAt, ledgerEntries));
    }
}

public static class TransactionErrors
{
    public const string AtLeastTwoEntries = "transaction.at_least_two_entries";
    public const string EntryAmountMustBePositive = "transaction.entry_amount_must_be_positive";
    public const string MixedCurrencies = "transaction.mixed_currencies";
    public const string Unbalanced = "transaction.unbalanced";
}
