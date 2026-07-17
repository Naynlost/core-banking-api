using Banking.Application.Messaging;

namespace Banking.Application.Accounts.GetStatement;

/// <summary>
/// One page of the account's ledger entries, newest first. Same ownership rule
/// as GetAccount: a foreign account is reported as not found.
/// </summary>
public sealed record GetAccountStatementQuery(
    Guid AccountId,
    string Requester,
    int Page = 1,
    int PageSize = 20) : IQuery<AccountStatementResponse>;

public sealed record AccountStatementResponse(
    IReadOnlyList<StatementEntryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>For a customer account a credit is money in, a debit is money out.</summary>
public sealed record StatementEntryResponse(
    Guid TransactionId,
    string Description,
    string Direction,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset OccurredAt);

public static class StatementErrors
{
    public const string PageOutOfRange = "statement.page_out_of_range";
    public const string PageSizeOutOfRange = "statement.page_size_out_of_range";
}
