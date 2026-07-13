using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

/// <summary>
/// The rules of a customer-to-customer transfer, independent of where the
/// entries are stored. The caller supplies the source account's current balance
/// and the total it already sent today (both derived from the ledger); on
/// success a balanced transaction is returned: the source is debited, the
/// destination credited, cash accounts untouched.
/// </summary>
public static class TransferPolicy
{
    /// <summary>Description shared by all transfer transactions; the daily limit is computed over it.</summary>
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

        // Only the sender needs completed KYC; receiving money stays allowed.
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
