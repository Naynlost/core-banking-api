namespace Banking.Application.Abstractions;

/// <summary>
/// Thrown by the unit of work when an optimistic concurrency token was stale,
/// i.e. another operation modified the same row between read and save.
/// </summary>
public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The operation conflicted with a concurrent update.", innerException);

/// <summary>
/// Thrown by the unit of work when a unique constraint was violated —
/// for idempotency keys this means the same key was committed concurrently.
/// </summary>
public sealed class UniqueConstraintViolationException(string? constraintName, Exception innerException)
    : Exception($"A unique constraint was violated{(constraintName is null ? "." : $": {constraintName}.")}", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
