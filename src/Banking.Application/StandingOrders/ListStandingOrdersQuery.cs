using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Domain.Primitives;

namespace Banking.Application.StandingOrders;

// Önce aktif olanlar, sonra en yeni önce sıralanır
public sealed record ListStandingOrdersQuery(string Requester) : IQuery<IReadOnlyList<StandingOrderResponse>>;

public sealed record StandingOrderResponse(
    Guid Id,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string CurrencyCode,
    string Frequency,
    string Status,
    DateTimeOffset NextRunAt,
    DateTimeOffset? LastRunAt,
    string? LastRunError);

internal sealed class ListStandingOrdersQueryHandler(IStandingOrderRepository standingOrders)
    : IQueryHandler<ListStandingOrdersQuery, IReadOnlyList<StandingOrderResponse>>
{
    public async Task<Result<IReadOnlyList<StandingOrderResponse>>> HandleAsync(
        ListStandingOrdersQuery query, CancellationToken cancellationToken)
    {
        var orders = await standingOrders.GetByOwnerAsync(query.Requester, cancellationToken);

        IReadOnlyList<StandingOrderResponse> items = orders
            .OrderBy(order => order.Status)
            .ThenByDescending(order => order.CreatedAt)
            .Select(order => new StandingOrderResponse(
                order.Id,
                order.SourceAccountId.Value,
                order.DestinationAccountId.Value,
                order.Amount.Amount,
                order.Amount.Currency.Code,
                order.Frequency.ToString(),
                order.Status.ToString(),
                order.NextRunAt,
                order.LastRunAt,
                order.LastRunError))
            .ToList();

        return Result.Success(items);
    }
}
