using Banking.Application.Messaging;

namespace Banking.Application.CashOperations;

// Aynı (requester, idempotency key) çifti en fazla bir kez uygulanır
public sealed record DepositMoneyCommand(
    string IdempotencyKey,
    string Requester,
    Guid AccountId,
    decimal Amount,
    string CurrencyCode) : ICommand<Guid>;

public sealed record WithdrawMoneyCommand(
    string IdempotencyKey,
    string Requester,
    Guid AccountId,
    decimal Amount,
    string CurrencyCode) : ICommand<Guid>;

public static class CashOperationErrors
{
    public const string Conflict = "cash_operation.conflict";
}
