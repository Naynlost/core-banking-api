using Banking.Application.Messaging;

namespace Banking.Application.Transfers;

/// <summary>
/// Moves money between two customer accounts; returns the ledger transaction id.
/// The same (requester, idempotency key) pair is applied at most once.
/// </summary>
public sealed record TransferMoneyCommand(
    string IdempotencyKey,
    string Requester,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string CurrencyCode) : ICommand<Guid>;

public static class TransferErrors
{
    /// <summary>Optimistic concurrency retries were exhausted; the client may retry.</summary>
    public const string Conflict = "transfer.conflict";
}
