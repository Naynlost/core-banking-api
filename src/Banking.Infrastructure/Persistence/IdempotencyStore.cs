using Banking.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Persistence;

internal sealed class IdempotencyStore(BankingDbContext context) : IIdempotencyStore
{
    public Task<IdempotencyRecord?> GetAsync(string key, string userId, CancellationToken cancellationToken) =>
        context.Set<IdempotencyRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Key == key && r.UserId == userId, cancellationToken);

    public async Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken) =>
        await context.Set<IdempotencyRecord>().AddAsync(record, cancellationToken);
}
