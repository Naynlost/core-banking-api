using Banking.Application.Abstractions;
using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(BankingDbContext context) : IAccountRepository
{
    public async Task<Account?> GetByIdAsync(AccountId id, CancellationToken cancellationToken) =>
        await context.Accounts.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Account>> GetByOwnerAsync(string owner, CancellationToken cancellationToken) =>
        await context.Accounts
            .Where(a => a.Owner == owner)
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);

    public async Task<Account?> GetCashAccountAsync(Currency currency, CancellationToken cancellationToken) =>
        // Single değil FirstOrDefault: eş zamanlı ilk hareket ikinci bir kasa hesabı yaratabilir
        await context.Accounts
            .Where(a => a.Owner == Account.SystemOwner
                && a.Type == AccountType.Asset
                && a.Currency == currency)
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(Account account, CancellationToken cancellationToken) =>
        await context.Accounts.AddAsync(account, cancellationToken);
}
