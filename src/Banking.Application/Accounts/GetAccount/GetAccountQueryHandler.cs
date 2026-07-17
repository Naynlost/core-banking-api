using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.Primitives;

namespace Banking.Application.Accounts.GetAccount;

internal sealed class GetAccountQueryHandler(
    IAccountRepository accounts,
    ITransactionRepository transactions) : IQueryHandler<GetAccountQuery, AccountResponse>
{
    public async Task<Result<AccountResponse>> HandleAsync(GetAccountQuery query, CancellationToken cancellationToken)
    {
        var account = await accounts.GetByIdAsync(new AccountId(query.AccountId), cancellationToken);

        if (account is null || account.Owner != query.Requester)
        {
            return Result.Failure<AccountResponse>(AccountApplicationErrors.NotFound);
        }

        return Result.Success(await AccountResponses.FromAsync(account, transactions, cancellationToken));
    }
}

internal static class AccountResponses
{
    public static async Task<AccountResponse> FromAsync(
        Account account, ITransactionRepository transactions, CancellationToken cancellationToken)
    {
        var totals = await transactions.GetEntryTotalsAsync(account.Id, cancellationToken);
        var balance = LedgerMath.Balance(account, totals.Debits, totals.Credits);

        return new AccountResponse(
            account.Id.Value,
            account.Currency.Code,
            account.Type.ToString(),
            account.Status.ToString(),
            account.KycStatus.ToString(),
            account.DailyTransferLimit,
            balance.Amount);
    }
}
