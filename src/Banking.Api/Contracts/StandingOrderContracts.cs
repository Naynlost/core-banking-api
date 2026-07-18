namespace Banking.Api.Contracts;

/// <summary>
/// Frequency is "Daily", "Weekly" or "Monthly". FirstRunAt omitted means the
/// first occurrence is due immediately.
/// </summary>
public sealed record CreateStandingOrderRequest(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string CurrencyCode,
    string Frequency,
    DateTimeOffset? FirstRunAt = null);

public sealed record CreateStandingOrderResponse(Guid Id);
