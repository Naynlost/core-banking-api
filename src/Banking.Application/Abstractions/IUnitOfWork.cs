namespace Banking.Application.Abstractions;

/// <summary>
/// Commits all pending changes of the current business operation atomically.
/// Repositories only stage changes; nothing hits the database until this is called.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
