namespace Banking.Api.Contracts;

public sealed record TransferRequest(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string CurrencyCode);

public sealed record TransferResponse(Guid TransactionId);
