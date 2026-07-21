using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Domain.Fraud;
using Banking.Domain.Primitives;

namespace Banking.Application.Fraud.ListFraudAlerts;

internal sealed class ListFraudAlertsQueryHandler(IFraudAlertRepository alerts)
    : IQueryHandler<ListFraudAlertsQuery, FraudAlertListResponse>
{
    public async Task<Result<FraudAlertListResponse>> HandleAsync(
        ListFraudAlertsQuery query, CancellationToken cancellationToken)
    {
        // Validator zaten parse edilemeyen filtreleri reddetmişti
        FraudAlertStatus? status = string.IsNullOrWhiteSpace(query.Status)
            ? null
            : Enum.Parse<FraudAlertStatus>(query.Status, ignoreCase: true);

        var page = await alerts.ListAsync(
            status, (query.Page - 1) * query.PageSize, query.PageSize, cancellationToken);

        var items = page.Alerts
            .Select(alert => new FraudAlertResponse(
                alert.Id,
                alert.TransactionId.Value,
                alert.Rule,
                alert.Detail,
                alert.FlaggedAt,
                alert.Status.ToString(),
                alert.ResolvedAt,
                alert.ResolutionNote))
            .ToList();

        return Result.Success(new FraudAlertListResponse(items, query.Page, query.PageSize, page.TotalCount));
    }
}
