namespace Banking.Application.Accounts;

public static class AccountApplicationErrors
{
    public const string NotFound = "account.not_found";

    /// <summary>The account was modified concurrently; the client may retry.</summary>
    public const string Conflict = "account.conflict";
}
