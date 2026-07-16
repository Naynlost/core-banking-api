namespace Banking.Domain.Events;

/// <summary>
/// Raised after a customer-to-customer transfer is committed to the ledger.
/// Goes out through the outbox, so the event exists exactly when the transfer does.
/// </summary>
public sealed record MoneyTransferred(
    Guid TransactionId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset OccurredAt);
