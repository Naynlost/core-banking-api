namespace Banking.Application.Abstractions;

// Optimistic concurrency token bayatlamış: aynı satır okuma ile kayıt arasında başka biri değiştirmiş
public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The operation conflicted with a concurrent update.", innerException);

// Idempotency key'de aynı key eş zamanlı iki kez commit edilmeye çalışılmış
public sealed class UniqueConstraintViolationException(string? constraintName, Exception innerException)
    : Exception($"A unique constraint was violated{(constraintName is null ? "." : $": {constraintName}.")}", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
