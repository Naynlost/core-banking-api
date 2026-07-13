using Banking.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Banking.Infrastructure.Persistence;

internal sealed class UnitOfWork(BankingDbContext context) : IUnitOfWork
{
    /// <summary>
    /// Persists all staged changes atomically. Provider-specific failures are
    /// translated to the application-level exceptions handlers know how to react to.
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            throw new UniqueConstraintViolationException(postgres.ConstraintName, exception);
        }
    }
}
