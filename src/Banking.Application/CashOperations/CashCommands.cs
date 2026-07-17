using Banking.Application.Messaging;

namespace Banking.Application.CashOperations;

/// <summary>
/// Books cash into the account, double-entry against the bank's cash account.
/// The same (requester, idempotency key) pair is applied at most once.
/// </summary>
public sealed record DepositMoneyCommand(
    string IdempotencyKey,
    string Requester,
    Guid AccountId,
    decimal Amount,
    string CurrencyCode) : ICommand<Guid>;

/// <summary>
/// Books cash out of the account, double-entry against the bank's cash account.
/// The same (requester, idempotency key) pair is applied at most once.
/// </summary>
public sealed record WithdrawMoneyCommand(
    string IdempotencyKey,
    string Requester,
    Guid AccountId,
    decimal Amount,
    string CurrencyCode) : ICommand<Guid>;

public static class CashOperationErrors
{
    /// <summary>Optimistic concurrency retries were exhausted; the client may retry.</summary>
    public const string Conflict = "cash_operation.conflict";
}
