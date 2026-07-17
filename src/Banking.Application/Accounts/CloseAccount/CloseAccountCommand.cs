using Banking.Application.Messaging;

namespace Banking.Application.Accounts.CloseAccount;

/// <summary>
/// Closes the requester's account. Only allowed when the ledger balance is
/// zero — otherwise money would be stranded on a closed account.
/// </summary>
public sealed record CloseAccountCommand(Guid AccountId, string Requester) : ICommand;
