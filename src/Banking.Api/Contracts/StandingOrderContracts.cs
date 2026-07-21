namespace Banking.Api.Contracts;

// Frequency "Daily", "Weekly" veya "Monthly"; FirstRunAt boşsa ilk tekrar hemen vadeye girer
public sealed record CreateStandingOrderRequest(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string CurrencyCode,
    string Frequency,
    DateTimeOffset? FirstRunAt = null);

public sealed record CreateStandingOrderResponse(Guid Id);
