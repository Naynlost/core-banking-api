using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

/// <summary>
/// How a balance is derived from ledger entry totals: assets grow with debits,
/// liabilities with credits. Single home for the sign rule so the in-memory
/// ledger and the persistence layer can never disagree.
/// </summary>
public static class LedgerMath
{
    public static Money Balance(Account account, decimal totalDebits, decimal totalCredits)
    {
        var net = account.Type == AccountType.Asset
            ? totalDebits - totalCredits
            : totalCredits - totalDebits;

        // Movement rules guarantee a non-negative balance; a negative net here
        // would mean a broken invariant, which Money.Create surfaces by failing.
        return Money.Create(net, account.Currency).Value;
    }
}
