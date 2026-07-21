using Banking.Application.Messaging;

namespace Banking.Application.Transfers;

// Aynı (requester, idempotency key) çifti en fazla bir kez uygulanır
public sealed record TransferMoneyCommand(
    string IdempotencyKey,
    string Requester,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string CurrencyCode) : ICommand<Guid>;

public static class TransferErrors
{
    public const string Conflict = "transfer.conflict";
}
