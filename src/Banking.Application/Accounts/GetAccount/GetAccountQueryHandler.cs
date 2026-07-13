using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Primitives;

namespace Banking.Application.Accounts.GetAccount;

internal sealed class GetAccountQueryHandler(IAccountRepository accounts)
    : IQueryHandler<GetAccountQuery, AccountResponse>
{
    public async Task<Result<AccountResponse>> HandleAsync(GetAccountQuery query, CancellationToken cancellationToken)
    {
        var account = await accounts.GetByIdAsync(new AccountId(query.AccountId), cancellationToken);

        if (account is null || account.Owner != query.Requester)
        {
            return Result.Failure<AccountResponse>(AccountApplicationErrors.NotFound);
        }

        return Result.Success(new AccountResponse(
            account.Id.Value,
            account.Currency.Code,
            account.Type.ToString(),
            account.Status.ToString(),
            account.KycStatus.ToString(),
            account.DailyTransferLimit));
    }
}
