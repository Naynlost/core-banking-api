namespace Banking.Application.Abstractions;

/// <summary>
/// One row per successfully executed idempotent operation, keyed per user.
/// The record is inserted in the same database transaction as the operation's
/// effects, so the unique key makes double-execution impossible even when the
/// same key arrives concurrently: the second insert violates the key and rolls
/// its whole transaction back.
/// </summary>
public sealed record IdempotencyRecord(
    string Key,
    string UserId,
    Guid TransactionId,
    DateTimeOffset CreatedAt);

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> GetAsync(string key, string userId, CancellationToken cancellationToken);

    /// <summary>Stages the record; it is persisted by the unit of work together with the operation.</summary>
    Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken);
}
