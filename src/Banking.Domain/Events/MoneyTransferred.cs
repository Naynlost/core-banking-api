namespace Banking.Domain.Events;

// Outbox üzerinden yayınlanır, transferle aynı transaction'da yazılır
public sealed record MoneyTransferred(
    Guid TransactionId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset OccurredAt);
