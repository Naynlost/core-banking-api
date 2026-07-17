using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Primitives;

namespace Banking.Application.Accounts.GetStatement;

internal sealed class GetAccountStatementQueryHandler(
    IAccountRepository accounts,
    ITransactionRepository transactions) : IQueryHandler<GetAccountStatementQuery, AccountStatementResponse>
{
    public async Task<Result<AccountStatementResponse>> HandleAsync(
        GetAccountStatementQuery query, CancellationToken cancellationToken)
    {
        var account = await accounts.GetByIdAsync(new AccountId(query.AccountId), cancellationToken);
        if (account is null || account.Owner != query.Requester)
        {
            return Result.Failure<AccountStatementResponse>(AccountApplicationErrors.NotFound);
        }

        var page = await transactions.GetStatementAsync(
            account.Id, (query.Page - 1) * query.PageSize, query.PageSize, cancellationToken);

        var items = page.Lines
            .Select(line => new StatementEntryResponse(
                line.TransactionId,
                line.Description,
                line.Direction.ToString(),
                line.Amount,
                line.CurrencyCode,
                line.OccurredAt))
            .ToList();

        return Result.Success(new AccountStatementResponse(items, query.Page, query.PageSize, page.TotalCount));
    }
}
