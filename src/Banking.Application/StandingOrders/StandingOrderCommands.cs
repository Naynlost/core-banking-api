using Banking.Application.Messaging;

namespace Banking.Application.StandingOrders;

/// <summary>
/// Sets up a recurring transfer owned by the requester. FirstRunAt defaults to
/// "now": the first occurrence executes on the next executor pass. Returns the
/// standing order id.
/// </summary>
public sealed record CreateStandingOrderCommand(
    string Requester,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string CurrencyCode,
    string Frequency,
    DateTimeOffset? FirstRunAt = null) : ICommand<Guid>;

public sealed record CancelStandingOrderCommand(Guid StandingOrderId, string Requester) : ICommand;

public static class StandingOrderApplicationErrors
{
    public const string NotFound = "standing_order.not_found";
    public const string InvalidFrequency = "standing_order.invalid_frequency";
}
