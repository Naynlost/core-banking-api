using Banking.Application.Messaging;

namespace Banking.Application.StandingOrders;

// FirstRunAt varsayılan "şimdi": ilk tekrar bir sonraki executor turunda çalışır
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
