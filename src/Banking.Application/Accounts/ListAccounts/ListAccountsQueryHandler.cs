using Banking.Application.Abstractions;
using Banking.Application.Accounts.GetAccount;
using Banking.Application.Messaging;
using Banking.Domain.Primitives;

namespace Banking.Application.Accounts.ListAccounts;

internal sealed class ListAccountsQueryHandler(
    IAccountRepository accounts,
    ITransactionRepository transactions) : IQueryHandler<ListAccountsQuery, IReadOnlyList<AccountResponse>>
{
    public async Task<Result<IReadOnlyList<AccountResponse>>> HandleAsync(
        ListAccountsQuery query, CancellationToken cancellationToken)
    {
        var owned = await accounts.GetByOwnerAsync(query.Requester, cancellationToken);

        var responses = new List<AccountResponse>(owned.Count);
        foreach (var account in owned)
        {
            responses.Add(await AccountResponses.FromAsync(account, transactions, cancellationToken));
        }

        return Result.Success<IReadOnlyList<AccountResponse>>(responses);
    }
}
