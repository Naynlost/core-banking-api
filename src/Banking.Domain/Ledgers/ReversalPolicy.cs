using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

/// <summary>
/// The ledger is append-only, so a wrong transaction is never edited or deleted;
/// it is corrected by posting a new transaction with every entry flipped. Only an
/// account that was credited by the original may trigger the reversal (it gives
/// the money back), and its balance must still cover what it received. A reversal
/// cannot itself be reversed — that would just be the original again.
/// </summary>
public static class ReversalPolicy
{
    public const string ReversalDescription = "Reversal";

    public static Result<Transaction> Reverse(
        Transaction original,
        AccountId refundingAccountId,
        Money refundingBalance,
        IReadOnlyCollection<Account> involvedAccounts,
        DateTimeOffset occurredAt)
    {
        if (original.ReversesTransactionId is not null)
        {
            return Result.Failure<Transaction>(ReversalErrors.NotReversible);
        }

        if (involvedAccounts.Any(a => a.IsClosed))
        {
            return Result.Failure<Transaction>(AccountErrors.Closed);
        }

        var refunded = original.Entries
            .Where(e => e.AccountId == refundingAccountId && e.Direction == EntryDirection.Credit)
            .Sum(e => e.Amount.Amount);
        if (refunded == 0)
        {
            return Result.Failure<Transaction>(ReversalErrors.OnlyCreditedAccountCanReverse);
        }

        // The reversal debits the refunder by exactly what the original credited it.
        if (refundingBalance.Amount < refunded)
        {
            return Result.Failure<Transaction>(LedgerErrors.InsufficientFunds);
        }

        var flipped = original.Entries
            .Select(e => new EntrySpec(e.AccountId, e.Amount, Flip(e.Direction)))
            .ToList();

        return Transaction.Create(ReversalDescription, occurredAt, flipped, reverses: original.Id);
    }

    private static EntryDirection Flip(EntryDirection direction) =>
        direction == EntryDirection.Debit ? EntryDirection.Credit : EntryDirection.Debit;
}

public static class ReversalErrors
{
    /// <summary>A reversal cannot be reversed; post a new transaction instead.</summary>
    public const string NotReversible = "transaction.not_reversible";

    public const string OnlyCreditedAccountCanReverse = "transaction.only_credited_account_can_reverse";

    public const string AlreadyReversed = "transaction.already_reversed";

    public const string NotFound = "transaction.not_found";
}
