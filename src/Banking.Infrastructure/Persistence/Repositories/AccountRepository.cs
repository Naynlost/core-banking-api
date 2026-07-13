using Banking.Application.Abstractions;
using Banking.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(BankingDbContext context) : IAccountRepository
{
    public async Task<Account?> GetByIdAsync(AccountId id, CancellationToken cancellationToken) =>
        await context.Accounts.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task AddAsync(Account account, CancellationToken cancellationToken) =>
        await context.Accounts.AddAsync(account, cancellationToken);
}
