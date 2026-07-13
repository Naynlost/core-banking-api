namespace Banking.Domain.Events;

/// <summary>
/// Raised when a customer-to-customer transfer has been committed to the ledger.
/// Published to the message broker through the outbox, so it exists if and only
/// if the transfer itself exists.
/// </summary>
public sealed record MoneyTransferred(
    Guid TransactionId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset OccurredAt);
