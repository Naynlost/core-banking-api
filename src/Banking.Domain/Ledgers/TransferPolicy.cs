using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

/// <summary>
/// The rules for customer-to-customer transfers, independent of where the
/// entries are stored. The caller passes in the source account's current
/// balance and what it has already sent today (both come from the ledger).
/// On success you get a balanced transaction back: source debited, destination
/// credited. Cash accounts are not involved in transfers.
/// </summary>
public static class TransferPolicy
{
    /// <summary>Every transfer transaction uses this description; the daily limit query filters on it.</summary>
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

        // Only the sender needs KYC; receiving money is always allowed.
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
