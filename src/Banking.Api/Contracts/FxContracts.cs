namespace Banking.Api.Contracts;

public sealed record FundFxPositionRequest(decimal Amount, string CurrencyCode);

public sealed record FundFxPositionResponse(Guid TransactionId);
