using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

/// <summary>
/// The append-only book of transactions. Validates movements (account open,
/// matching currency, positive amount, enough funds) and calculates balances
/// from the entries, since accounts don't store a balance themselves.
/// Deposits and withdrawals are booked double-entry against the bank's cash
/// account for that currency.
/// </summary>
public sealed class Ledger
{
    private readonly List<Transaction> _transactions = [];
    private readonly Dictionary<Currency, Account> _cashAccounts = [];

    public IReadOnlyList<Transaction> Transactions => _transactions;

    /// <summary>The bank's own cash (asset) account for the given currency.</summary>
    public Account CashAccount(Currency currency)
    {
        if (!_cashAccounts.TryGetValue(currency, out var cash))
        {
            cash = Account.OpenCash(currency);
            _cashAccounts[currency] = cash;
        }

        return cash;
    }

    public Result<Transaction> Deposit(Account account, Money amount, DateTimeOffset occurredAt)
    {
        var validation = ValidateMovement(account, amount);
        if (validation.IsFailure)
        {
            return Result.Failure<Transaction>(validation.Error);
        }

        var cash = CashAccount(account.Currency);
        return Post(Transaction.Create(
            "Deposit",
            occurredAt,
            [
                new EntrySpec(cash.Id, amount, EntryDirection.Debit),
                new EntrySpec(account.Id, amount, EntryDirection.Credit),
            ]));
    }

    public Result<Transaction> Withdraw(Account account, Money amount, DateTimeOffset occurredAt)
    {
        var validation = ValidateMovement(account, amount);
        if (validation.IsFailure)
        {
            return Result.Failure<Transaction>(validation.Error);
        }

        var balance = GetBalance(account);
        if (balance.Amount < amount.Amount)
        {
            return Result.Failure<Transaction>(LedgerErrors.InsufficientFunds);
        }

        var cash = CashAccount(account.Currency);
        return Post(Transaction.Create(
            "Withdrawal",
            occurredAt,
            [
                new EntrySpec(account.Id, amount, EntryDirection.Debit),
                new EntrySpec(cash.Id, amount, EntryDirection.Credit),
            ]));
    }

    public Result<Transaction> Transfer(Account source, Account destination, Money amount, DateTimeOffset occurredAt) =>
        Post(TransferPolicy.Transfer(
            source, GetBalance(source), GetTransferredOnDay(source, occurredAt), destination, amount, occurredAt));

    /// <summary>How much the account has already sent by transfer on the given UTC day.</summary>
    public Money GetTransferredOnDay(Account account, DateTimeOffset day)
    {
        var utcDate = day.UtcDateTime.Date;
        var total = _transactions
            .Where(t => t.Description == TransferPolicy.TransferDescription)
            .SelectMany(t => t.Entries)
            .Where(e => e.AccountId == account.Id
                && e.Direction == EntryDirection.Debit
                && e.OccurredAt.UtcDateTime.Date == utcDate)
            .Sum(e => e.Amount.Amount);

        return Money.Create(total, account.Currency).Value;
    }

    /// <summary>Calculates the account's balance from its ledger entries.</summary>
    public Money GetBalance(Account account)
    {
        decimal debits = 0;
        decimal credits = 0;

        foreach (var entry in _transactions.SelectMany(t => t.Entries))
        {
            if (entry.AccountId != account.Id)
            {
                continue;
            }

            if (entry.Direction == EntryDirection.Debit)
            {
                debits += entry.Amount.Amount;
            }
            else
            {
                credits += entry.Amount.Amount;
            }
        }

        return LedgerMath.Balance(account, debits, credits);
    }

    private Result<Transaction> Post(Result<Transaction> transaction)
    {
        if (transaction.IsSuccess)
        {
            _transactions.Add(transaction.Value);
        }

        return transaction;
    }

    private static Result ValidateMovement(Account account, Money amount)
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

public static class LedgerErrors
{
    public const string CurrencyMismatch = "ledger.currency_mismatch";
    public const string AmountMustBePositive = "ledger.amount_must_be_positive";
    public const string InsufficientFunds = "ledger.insufficient_funds";
    public const string SameAccount = "ledger.same_account";
    public const string DailyLimitExceeded = "ledger.daily_limit_exceeded";
}
