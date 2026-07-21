using Banking.Application.Messaging;

namespace Banking.Application.Accounts.GetAccount;

// Başkasının hesabı 404 döner, varlığı hiç sızdırılmaz
public sealed record GetAccountQuery(Guid AccountId, string Requester) : IQuery<AccountResponse>;

// Bakiye saklanmaz, her okumada ledger'dan türetilir
public sealed record AccountResponse(
    Guid Id,
    string Currency,
    string Type,
    string Status,
    string KycStatus,
    decimal DailyTransferLimit,
    decimal Balance);
