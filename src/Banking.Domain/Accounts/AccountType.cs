namespace Banking.Domain.Accounts;

/// <summary>
/// Accounting side of an account, which determines how its balance is derived
/// from ledger entries (its "normal balance"):
/// Asset accounts (the bank's cash) grow with debits, liability accounts
/// (customer deposits, money the bank owes) grow with credits.
/// </summary>
public enum AccountType
{
    Asset,
    Liability,
}
