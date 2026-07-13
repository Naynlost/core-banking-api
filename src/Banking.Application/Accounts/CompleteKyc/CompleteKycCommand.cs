using Banking.Application.Messaging;

namespace Banking.Application.Accounts.CompleteKyc;

/// <summary>
/// Marks the requester's account as KYC-verified. Stands in for a real
/// verification flow (document checks, back-office approval); the domain only
/// cares about the resulting status.
/// </summary>
public sealed record CompleteKycCommand(Guid AccountId, string Requester) : ICommand;
