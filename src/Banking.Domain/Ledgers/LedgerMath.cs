using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

/// <summary>
/// The sign rule for turning entry totals into a balance: assets grow with
/// debits, liabilities with credits. Kept in one place so the in-memory ledger
/// and the persistence layer can't drift apart.
/// </summary>
public static class LedgerMath
{
    public static Money Balance(Account account, decimal totalDebits, decimal totalCredits)
    {
        var net = account.Type == AccountType.Asset
            ? totalDebits - totalCredits
            : totalCredits - totalDebits;

        // The movement rules should make a negative balance impossible. If net
        // is negative something is broken, and Money.Create will fail loudly.
        return Money.Create(net, account.Currency).Value;
    }
}
