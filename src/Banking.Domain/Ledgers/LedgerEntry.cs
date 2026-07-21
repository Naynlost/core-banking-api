using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

// Değişmez satır; hata olursa güncelleme değil, ters kayıt (reversal) atılır
public sealed record LedgerEntry
{
    // EF'in nesne oluşturması için, veri yazılırken zaten doğrulanmıştı
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

    // Her zaman pozitif; yönü (borç/alacak) Direction belirler
    public Money Amount { get; }

    public EntryDirection Direction { get; }

    public DateTimeOffset OccurredAt { get; }

    // Internal: satır sadece Transaction.Create üzerinden, dengeli bir işlemin parçası olarak oluşur
    internal static LedgerEntry Create(
        TransactionId transactionId,
        AccountId accountId,
        Money amount,
        EntryDirection direction,
        DateTimeOffset occurredAt) =>
        new(Guid.NewGuid(), transactionId, accountId, amount, direction, occurredAt);
}
