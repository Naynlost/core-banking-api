using Banking.Domain.Fraud;

namespace Banking.Application.Abstractions;

public interface IFraudAlertRepository
{
    /// <summary>One page of alerts, newest first, optionally filtered by status.</summary>
    Task<FraudAlertPage> ListAsync(
        FraudAlertStatus? status, int skip, int take, CancellationToken cancellationToken);

    Task<FraudAlert?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record FraudAlertPage(IReadOnlyList<FraudAlert> Alerts, int TotalCount);
