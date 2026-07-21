using Banking.Application.Messaging;

namespace Banking.Application.Accounts.GetStatement;

// En yeni önce; GetAccount'taki gibi başkasının hesabı 404 döner
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

// Müşteri hesabında credit para girişi, debit para çıkışıdır
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
