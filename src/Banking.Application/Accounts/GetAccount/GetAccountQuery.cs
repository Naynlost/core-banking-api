using Banking.Application.Messaging;

namespace Banking.Application.Accounts.GetAccount;

/// <summary>
/// Returns the account only if it belongs to the requester; a foreign account
/// is reported as not found so its existence is never leaked.
/// </summary>
public sealed record GetAccountQuery(Guid AccountId, string Requester) : IQuery<AccountResponse>;

public sealed record AccountResponse(
    Guid Id,
    string Currency,
    string Type,
    string Status,
    string KycStatus,
    decimal DailyTransferLimit);
