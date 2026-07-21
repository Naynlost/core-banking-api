using Banking.Application.Accounts.GetAccount;
using Banking.Application.Messaging;

namespace Banking.Application.Accounts.ListAccounts;

public sealed record ListAccountsQuery(string Requester) : IQuery<IReadOnlyList<AccountResponse>>;
