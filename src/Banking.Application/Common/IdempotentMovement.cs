using Banking.Application.Abstractions;
using Banking.Domain.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Application.Common;

/// <summary>
/// The shared skeleton of every idempotent money movement (transfer, deposit,
/// withdrawal): replay the stored result when the idempotency key is known,
/// otherwise run the attempt; retry on optimistic-concurrency conflicts against
/// fresh state, and when the same key was committed concurrently, return the
/// winner's result. Each attempt gets its own DI scope, because a DbContext
/// whose save failed still tracks the rejected changes.
/// </summary>
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
                // Another movement touched one of the accounts; retry on fresh state.
            }
            catch (ConcurrencyConflictException)
            {
                return Result.Failure<Guid>(conflictError);
            }
            catch (UniqueConstraintViolationException)
            {
                // The same idempotency key was committed concurrently: our attempt
                // rolled back with the failed insert, so return the committed outcome.
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
