namespace Banking.Application.Accounts;

public static class AccountApplicationErrors
{
    public const string NotFound = "account.not_found";

    // Hesap eş zamanlı değiştirilmiş, istemci tekrar deneyebilir
    public const string Conflict = "account.conflict";
}
