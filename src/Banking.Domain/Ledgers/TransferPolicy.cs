using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

// Müşteriden müşteriye transfer kuralları; bakiye ve günlük gönderim ledger'dan gelir
public static class TransferPolicy
{
    // Günlük limit sorgusu bu açıklamaya göre filtreler
    public const string TransferDescription = "Transfer";

    public static Result<Transaction> Transfer(
        Account source,
        Money sourceBalance,
        Money transferredToday,
        Account destination,
        Money amount,
        DateTimeOffset occurredAt)
    {
        if (source.Id == destination.Id)
        {
            return Result.Failure<Transaction>(LedgerErrors.SameAccount);
        }

        if (source.IsClosed || destination.IsClosed)
        {
            return Result.Failure<Transaction>(AccountErrors.Closed);
        }

        // Sadece gönderen için KYC şartı var, alım her zaman serbest
        if (!source.IsKycVerified)
        {
            return Result.Failure<Transaction>(AccountErrors.KycNotVerified);
        }

        if (amount.Currency != source.Currency || amount.Currency != destination.Currency)
        {
            return Result.Failure<Transaction>(LedgerErrors.CurrencyMismatch);
        }

        if (amount.IsZero)
        {
            return Result.Failure<Transaction>(LedgerErrors.AmountMustBePositive);
        }

        if (sourceBalance.Amount < amount.Amount)
        {
            return Result.Failure<Transaction>(LedgerErrors.InsufficientFunds);
        }

        if (transferredToday.Amount + amount.Amount > source.DailyTransferLimit)
        {
            return Result.Failure<Transaction>(LedgerErrors.DailyLimitExceeded);
        }

        return Transaction.Create(
            TransferDescription,
            occurredAt,
            [
                new EntrySpec(source.Id, amount, EntryDirection.Debit),
                new EntrySpec(destination.Id, amount, EntryDirection.Credit),
            ]);
    }
}
