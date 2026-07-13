namespace Banking.Application.Abstractions;

/// <summary>
/// Stages a domain event for publication. The event row is persisted by the unit
/// of work in the SAME database transaction as the operation that raised it, so
/// an event can never exist without its operation or get lost with it — a
/// background publisher pushes staged rows to the broker afterwards (outbox pattern).
/// </summary>
public interface IOutbox
{
    Task EnqueueAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : class;
}
