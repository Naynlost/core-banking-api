using Banking.Application.Abstractions;
using Banking.Domain.StandingOrders;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Persistence.Repositories;

internal sealed class StandingOrderRepository(BankingDbContext context) : IStandingOrderRepository
{
    public async Task AddAsync(StandingOrder order, CancellationToken cancellationToken) =>
        await context.StandingOrders.AddAsync(order, cancellationToken);

    public async Task<StandingOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await context.StandingOrders.FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public async Task<IReadOnlyList<StandingOrder>> GetByOwnerAsync(
        string owner, CancellationToken cancellationToken) =>
        await context.StandingOrders
            .Where(order => order.Owner == owner)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StandingOrder>> GetDueAsync(
        DateTimeOffset now, int take, CancellationToken cancellationToken) =>
        await context.StandingOrders
            .Where(order => order.Status == StandingOrderStatus.Active && order.NextRunAt <= now)
            .OrderBy(order => order.NextRunAt)
            .Take(take)
            .ToListAsync(cancellationToken);
}
