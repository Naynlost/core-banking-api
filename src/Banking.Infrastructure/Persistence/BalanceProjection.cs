using Banking.Application.Abstractions;
using Banking.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Persistence;

internal sealed class BalanceProjection(BankingDbContext context) : IBalanceProjection
{
    public async Task<EntryTotals> GetTotalsAsync(AccountId accountId, CancellationToken cancellationToken)
    {
        var row = await context.AccountBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(balance => balance.AccountId == accountId, cancellationToken);

        return row is null ? default : new EntryTotals(row.Debits, row.Credits);
    }
}
