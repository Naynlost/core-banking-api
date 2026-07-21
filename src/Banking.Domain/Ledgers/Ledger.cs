using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

// Değişmez (append-only) işlem defteri; bakiye hesapta değil, buradaki kayıtlarda tutulur
public sealed class Ledger
{
    private readonly List<Transaction> _transactions = [];
    private readonly Dictionary<Currency, Account> _cashAccounts = [];

    public IReadOnlyList<Transaction> Transactions => _transactions;

    public Account CashAccount(Currency currency)
    {
        if (!_cashAccounts.TryGetValue(currency, out var cash))
        {
            cash = Account.OpenCash(currency);
            _cashAccounts[currency] = cash;
        }

        return cash;
    }

    public Result<Transaction> Deposit(Account account, Money amount, DateTimeOffset occurredAt) =>
        Post(CashPolicy.Deposit(account, CashAccount(account.Currency), amount, occurredAt));

    public Result<Transaction> Withdraw(Account account, Money amount, DateTimeOffset occurredAt) =>
        Post(CashPolicy.Withdraw(account, CashAccount(account.Currency), GetBalance(account), amount, occurredAt));

    public Result<Transaction> Transfer(Account source, Account destination, Money amount, DateTimeOffset occurredAt) =>
        Post(TransferPolicy.Transfer(
            source, GetBalance(source), GetTransferredOnDay(source, occurredAt), destination, amount, occurredAt));

    public Result<Transaction> Reverse(
        Transaction original,
        Account refundingAccount,
        IReadOnlyCollection<Account> involvedAccounts,
        DateTimeOffset occurredAt)
    {
        if (_transactions.Any(t => t.ReversesTransactionId == original.Id))
        {
            return Result.Failure<Transaction>(ReversalErrors.AlreadyReversed);
        }

        return Post(ReversalPolicy.Reverse(
            original, refundingAccount.Id, GetBalance(refundingAccount), involvedAccounts, occurredAt));
    }

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
}

public static class LedgerErrors
{
    public const string CurrencyMismatch = "ledger.currency_mismatch";
    public const string AmountMustBePositive = "ledger.amount_must_be_positive";
    public const string InsufficientFunds = "ledger.insufficient_funds";
    public const string SameAccount = "ledger.same_account";
    public const string DailyLimitExceeded = "ledger.daily_limit_exceeded";
}
