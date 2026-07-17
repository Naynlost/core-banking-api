using Banking.Application.Messaging;

namespace Banking.Application.Transactions.ReverseTransaction;

/// <summary>
/// Posts the reversal of a transaction; returns the reversal's transaction id.
/// The requester must own an account the original transaction credited (it is
/// the one giving the money back). No idempotency key: the unique index on the
/// reversal link makes a second reversal impossible by construction.
/// </summary>
public sealed record ReverseTransactionCommand(Guid TransactionId, string Requester) : ICommand<Guid>;
