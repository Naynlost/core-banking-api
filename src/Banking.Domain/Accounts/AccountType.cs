namespace Banking.Domain.Accounts;

/// <summary>
/// Which side of the books the account sits on, i.e. how its balance is read
/// from the ledger: asset accounts (the bank's own cash) grow with debits,
/// liability accounts (customer deposits, money the bank owes) grow with credits.
/// </summary>
public enum AccountType
{
    Asset,
    Liability,
}
