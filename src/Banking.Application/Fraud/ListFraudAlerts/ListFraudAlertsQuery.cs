using Banking.Application.Messaging;

namespace Banking.Application.Fraud.ListFraudAlerts;

// En yeni önce; Status null ise tümü, değilse Open/Confirmed/Dismissed'e göre filtreler
public sealed record ListFraudAlertsQuery(
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IQuery<FraudAlertListResponse>;

public sealed record FraudAlertListResponse(
    IReadOnlyList<FraudAlertResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record FraudAlertResponse(
    Guid Id,
    Guid TransactionId,
    string Rule,
    string Detail,
    DateTimeOffset FlaggedAt,
    string Status,
    DateTimeOffset? ResolvedAt,
    string? ResolutionNote);
