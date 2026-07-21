using Banking.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Banking.Infrastructure.Persistence;

internal sealed class UnitOfWork(BankingDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            var translated = Translate(exception);
            if (ReferenceEquals(translated, exception))
            {
                throw;
            }

            throw translated;
        }
    }

    // Provider'a özgü hataları handler'ların bildiği application-level exception'lara çevirir.
    // Tanınmayan hata olduğu gibi geri döner ve çağıran onu orijinal stack trace'iyle yeniden fırlatır.
    internal static Exception Translate(DbUpdateException exception) => exception switch
    {
        DbUpdateConcurrencyException => new ConcurrencyConflictException(exception),

        { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres }
            => new UniqueConstraintViolationException(postgres.ConstraintName, exception),

        // Postgres eş zamanlı transaction'larda kilit döngüsünü kırmak için bir kurban seçer
        // (deadlock) ya da serialization hatası verir; ikisi de geçicidir, çakışma gibi yeniden denenir
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure,
            },
        }
            => new ConcurrencyConflictException(exception),

        _ => exception,
    };
}
