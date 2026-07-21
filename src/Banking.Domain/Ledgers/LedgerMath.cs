using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

// Asset hesap debit ile, liability hesap credit ile büyür; kural burada tek yerde tutulur
public static class LedgerMath
{
    public static Money Balance(Account account, decimal totalDebits, decimal totalCredits)
    {
        var net = account.Type == AccountType.Asset
            ? totalDebits - totalCredits
            : totalCredits - totalDebits;

        // Negatif bakiye normalde imkansız olmalı, olursa Money.Create hata fırlatır
        return Money.Create(net, account.Currency).Value;
    }
}
