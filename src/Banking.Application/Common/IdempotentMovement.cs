using Banking.Application.Abstractions;
using Banking.Domain.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Application.Common;

// Key biliniyorsa eski sonucu döner, yoksa dener; çakışmada taze DI scope'uyla tekrar dener
internal static class IdempotentMovement
{
    internal const int MaxAttempts = 3;

    public static async Task<Result<Guid>> ExecuteAsync(
        IServiceScopeFactory scopeFactory,
        string idempotencyKey,
        string userId,
        string conflictError,
        Func<IServiceProvider, CancellationToken, Task<Result<Guid>>> attemptAsync,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var idempotency = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

                var replay = await idempotency.GetAsync(idempotencyKey, userId, cancellationToken);
                if (replay is not null)
                {
                    return Result.Success(replay.TransactionId);
                }

                return await attemptAsync(scope.ServiceProvider, cancellationToken);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                // Başka bir hareket hesaplardan birine dokunmuş, taze state ile tekrar dene
            }
            catch (ConcurrencyConflictException)
            {
                return Result.Failure<Guid>(conflictError);
            }
            catch (UniqueConstraintViolationException)
            {
                // Aynı key eş zamanlı commit edilmiş: bizim insert rollback oldu, kazananın sonucunu döndür
                var stored = await GetStoredResultAsync(scopeFactory, idempotencyKey, userId, cancellationToken);
                if (stored is not null)
                {
                    return Result.Success(stored.TransactionId);
                }

                return Result.Failure<Guid>(conflictError);
            }
        }

        return Result.Failure<Guid>(conflictError);
    }

    private static async Task<IdempotencyRecord?> GetStoredResultAsync(
        IServiceScopeFactory scopeFactory, string key, string userId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var idempotency = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        return await idempotency.GetAsync(key, userId, cancellationToken);
    }
}
