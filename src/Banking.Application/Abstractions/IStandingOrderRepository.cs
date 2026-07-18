using Banking.Domain.StandingOrders;

namespace Banking.Application.Abstractions;

public interface IStandingOrderRepository
{
    Task AddAsync(StandingOrder order, CancellationToken cancellationToken);

    Task<StandingOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<StandingOrder>> GetByOwnerAsync(string owner, CancellationToken cancellationToken);

    /// <summary>Active orders whose next run is due at or before <paramref name="now"/>, oldest first.</summary>
    Task<IReadOnlyList<StandingOrder>> GetDueAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);
}
