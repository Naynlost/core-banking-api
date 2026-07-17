using Banking.Application.Accounts.GetAccount;
using Banking.Application.Messaging;

namespace Banking.Application.Accounts.ListAccounts;

/// <summary>The requester's own accounts, balances derived from the ledger.</summary>
public sealed record ListAccountsQuery(string Requester) : IQuery<IReadOnlyList<AccountResponse>>;
