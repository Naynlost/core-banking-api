using Banking.Application.Abstractions;
using Banking.Domain.Fraud;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Persistence.Repositories;

internal sealed class FraudAlertRepository(BankingDbContext context) : IFraudAlertRepository
{
    public async Task<FraudAlertPage> ListAsync(
        FraudAlertStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        var query = context.FraudAlerts.AsQueryable();
        if (status is not null)
        {
            query = query.Where(alert => alert.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Aynı zaman damgasını paylaşan uyarılarda sayfalar kaymasın diye Id ile tie-break
        var alerts = await query
            .OrderByDescending(alert => alert.FlaggedAt)
            .ThenBy(alert => alert.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new FraudAlertPage(alerts, totalCount);
    }

    public async Task<FraudAlert?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await context.FraudAlerts.FirstOrDefaultAsync(alert => alert.Id == id, cancellationToken);
}
