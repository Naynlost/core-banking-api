namespace Banking.Api.Contracts;

public sealed record CashOperationRequest(decimal Amount, string CurrencyCode);

public sealed record CashOperationResponse(Guid TransactionId);
