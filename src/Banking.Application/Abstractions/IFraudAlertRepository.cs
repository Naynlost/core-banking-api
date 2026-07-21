using Banking.Domain.Fraud;

namespace Banking.Application.Abstractions;

public interface IFraudAlertRepository
{
    // En yeni önce, isteğe bağlı status filtresiyle
    Task<FraudAlertPage> ListAsync(
        FraudAlertStatus? status, int skip, int take, CancellationToken cancellationToken);

    Task<FraudAlert?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record FraudAlertPage(IReadOnlyList<FraudAlert> Alerts, int TotalCount);
