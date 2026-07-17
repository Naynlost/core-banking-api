using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

/// <summary>
/// The rules for cash movements between a customer account and the bank's own
/// cash account: deposits and withdrawals. Like transfers, the caller supplies
/// the balance (it comes from the ledger); on success a balanced double-entry
/// transaction comes back. KYC is not required here — it only gates sending
/// transfers.
/// </summary>
public static class CashPolicy
{
    public const string DepositDescription = "Deposit";

    public const string WithdrawalDescription = "Withdrawal";

    public static Result<Transaction> Deposit(Account account, Account cash, Money amount, DateTimeOffset occurredAt)
    {
        var validation = Validate(account, amount);
        if (validation.IsFailure)
        {
            return Result.Failure<Transaction>(validation.Error);
        }

        return Transaction.Create(
            DepositDescription,
            occurredAt,
            [
                new EntrySpec(cash.Id, amount, EntryDirection.Debit),
                new EntrySpec(account.Id, amount, EntryDirection.Credit),
            ]);
    }

    public static Result<Transaction> Withdraw(
        Account account, Account cash, Money balance, Money amount, DateTimeOffset occurredAt)
    {
        var validation = Validate(account, amount);
        if (validation.IsFailure)
        {
            return Result.Failure<Transaction>(validation.Error);
        }

        if (balance.Amount < amount.Amount)
        {
            return Result.Failure<Transaction>(LedgerErrors.InsufficientFunds);
        }

        return Transaction.Create(
            WithdrawalDescription,
            occurredAt,
            [
                new EntrySpec(account.Id, amount, EntryDirection.Debit),
                new EntrySpec(cash.Id, amount, EntryDirection.Credit),
            ]);
    }

    private static Result Validate(Account account, Money amount)
    {
        if (account.IsClosed)
        {
            return Result.Failure(AccountErrors.Closed);
        }

        if (amount.Currency != account.Currency)
        {
            return Result.Failure(LedgerErrors.CurrencyMismatch);
        }

        if (amount.IsZero)
        {
            return Result.Failure(LedgerErrors.AmountMustBePositive);
        }

        return Result.Success();
    }
}
