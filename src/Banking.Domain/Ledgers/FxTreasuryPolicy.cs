using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Ledgers;

// Bankanın döviz pozisyonuna stok yüklemesi. Gerçek hayatta bunun karşılığı hazine
// biriminin döviz alımıdır; burada kasa hesabı borçlanır (banka o dövizi eline alır),
// pozisyon alacaklanır (döviz masasının kullanabileceği stok artar). İşlem tek para
// biriminde dengelidir ve iki hesabın bakiyesi de negatife düşmez.
public static class FxTreasuryPolicy
{
    public const string FundingDescription = "FX funding";

    public static Result<Transaction> Fund(
        Account cash, Account position, Money amount, DateTimeOffset occurredAt)
    {
        if (cash.Type != AccountType.Asset)
        {
            return Result.Failure<Transaction>(LedgerErrors.NotACashAccount);
        }

        if (position.Type != AccountType.FxPosition)
        {
            return Result.Failure<Transaction>(LedgerErrors.NotAnFxPosition);
        }

        if (amount.Currency != cash.Currency || amount.Currency != position.Currency)
        {
            return Result.Failure<Transaction>(LedgerErrors.CurrencyMismatch);
        }

        if (amount.IsZero)
        {
            return Result.Failure<Transaction>(LedgerErrors.AmountMustBePositive);
        }

        return Transaction.Create(
            FundingDescription,
            occurredAt,
            [
                new EntrySpec(cash.Id, amount, EntryDirection.Debit),
                new EntrySpec(position.Id, amount, EntryDirection.Credit),
            ]);
    }
}
