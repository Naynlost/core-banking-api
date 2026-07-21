using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

public readonly record struct EntrySpec(AccountId AccountId, Money Amount, EntryDirection Direction);

// En az iki satır, toplam borç toplam alacağa eşit (double-entry invariantı)
public sealed class Transaction
{
    private readonly List<LedgerEntry> _entries;

    // EF'in nesne oluşturması için, veri yazılırken zaten doğrulanmıştı
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

        // Denge para birimi BAŞINA aranır. Farklı birimler toplanamayacağı için çapraz kur
        // işlemi tek bir toplamda dengelenemez; her birim kendi içinde sıfırlanmalıdır.
        // Tek para birimli işlem bunun özel hâlidir.
        var netByCurrency = new Dictionary<Currency, decimal>();

        foreach (var entry in entries)
        {
            if (entry.Amount.IsZero)
            {
                return Result.Failure<Transaction>(TransactionErrors.EntryAmountMustBePositive);
            }

            var signed = entry.Direction == EntryDirection.Debit
                ? entry.Amount.Amount
                : -entry.Amount.Amount;

            netByCurrency[entry.Amount.Currency] =
                netByCurrency.GetValueOrDefault(entry.Amount.Currency) + signed;
        }

        if (netByCurrency.Values.Any(net => net != 0))
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
    public const string Unbalanced = "transaction.unbalanced";
}
