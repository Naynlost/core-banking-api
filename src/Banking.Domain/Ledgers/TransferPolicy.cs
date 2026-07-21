using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

// Çapraz kur transferinin ikinci bacağı için gereken her şey. Kur araması I/O olduğundan
// çevrimi çağıran yapar; politika yalnızca sonucu doğrular ve defterе yazar.
public sealed record FxContext(
    Money ConvertedAmount,
    Account SourcePosition,
    Account DestinationPosition,
    Money DestinationPositionBalance);

// Müşteriden müşteriye transfer kuralları; bakiye ve günlük gönderim ledger'dan gelir
public static class TransferPolicy
{
    // Günlük limit sorgusu bu açıklamaya göre filtreler
    public const string TransferDescription = "Transfer";

    // fx null ise iki hesap da aynı para biriminde olmalıdır. Doluysa işlem dört satır üretir:
    // gönderilen birim bankanın pozisyonuna girer, alınan birim pozisyondan çıkar; böylece
    // her para birimi kendi içinde dengelenir.
    public static Result<Transaction> Transfer(
        Account source,
        Money sourceBalance,
        Money transferredToday,
        Account destination,
        Money amount,
        DateTimeOffset occurredAt,
        FxContext? fx = null)
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

        // Tutar her zaman gönderenin para birimindedir; limit ve bakiye de onun üzerinden işler
        if (amount.Currency != source.Currency)
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

        return fx is null
            ? SameCurrency(source, destination, amount, occurredAt)
            : CrossCurrency(source, destination, amount, fx, occurredAt);
    }

    private static Result<Transaction> SameCurrency(
        Account source, Account destination, Money amount, DateTimeOffset occurredAt)
    {
        if (amount.Currency != destination.Currency)
        {
            return Result.Failure<Transaction>(LedgerErrors.CurrencyMismatch);
        }

        return Transaction.Create(
            TransferDescription,
            occurredAt,
            [
                new EntrySpec(source.Id, amount, EntryDirection.Debit),
                new EntrySpec(destination.Id, amount, EntryDirection.Credit),
            ]);
    }

    private static Result<Transaction> CrossCurrency(
        Account source, Account destination, Money amount, FxContext fx, DateTimeOffset occurredAt)
    {
        if (fx.ConvertedAmount.Currency != destination.Currency
            || fx.SourcePosition.Currency != source.Currency
            || fx.DestinationPosition.Currency != destination.Currency
            || fx.DestinationPositionBalance.Currency != destination.Currency)
        {
            return Result.Failure<Transaction>(LedgerErrors.CurrencyMismatch);
        }

        if (fx.SourcePosition.Type != AccountType.FxPosition
            || fx.DestinationPosition.Type != AccountType.FxPosition)
        {
            return Result.Failure<Transaction>(LedgerErrors.NotAnFxPosition);
        }

        // Banka ancak elinde bulunan dövizi satabilir; pozisyon yetmiyorsa transfer olmaz
        if (fx.DestinationPositionBalance.Amount < fx.ConvertedAmount.Amount)
        {
            return Result.Failure<Transaction>(LedgerErrors.InsufficientFxLiquidity);
        }

        return Transaction.Create(
            TransferDescription,
            occurredAt,
            [
                // Gönderilen para birimi: müşteriden çıkar, bankanın pozisyonuna girer
                new EntrySpec(source.Id, amount, EntryDirection.Debit),
                new EntrySpec(fx.SourcePosition.Id, amount, EntryDirection.Credit),

                // Alınan para birimi: bankanın pozisyonundan çıkar, müşteriye girer
                new EntrySpec(fx.DestinationPosition.Id, fx.ConvertedAmount, EntryDirection.Debit),
                new EntrySpec(destination.Id, fx.ConvertedAmount, EntryDirection.Credit),
            ]);
    }
}
