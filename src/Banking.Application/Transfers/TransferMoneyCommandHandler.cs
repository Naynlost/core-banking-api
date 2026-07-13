using Banking.Application.Abstractions;
using Banking.Application.Accounts;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Events;
using Banking.Domain.Ledgers;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Application.Transfers;

/// <summary>
/// Ledger entries are append-only, so two concurrent transfers never collide on
/// data; the conflict comes from both bumping the accounts' Version concurrency
/// token. On such a conflict the whole attempt is retried against fresh state —
/// each attempt runs in its own scope because a DbContext that failed to save
/// still tracks the rejected changes.
/// </summary>
internal sealed class TransferMoneyCommandHandler(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : ICommandHandler<TransferMoneyCommand, Guid>
{
    internal const int MaxAttempts = 3;

    public async Task<Result<Guid>> HandleAsync(TransferMoneyCommand command, CancellationToken cancellationToken)
    {
        var result = await ExecuteWithRetriesAsync(command, cancellationToken);
        BankingDiagnostics.Transfers.Add(
            1, new KeyValuePair<string, object?>("outcome", result.IsSuccess ? "success" : result.Error));
        return result;
    }

    private async Task<Result<Guid>> ExecuteWithRetriesAsync(
        TransferMoneyCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await ExecuteOnceAsync(command, cancellationToken);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                // Another movement touched one of the accounts; retry on fresh state.
            }
            catch (ConcurrencyConflictException)
            {
                return Result.Failure<Guid>(TransferErrors.Conflict);
            }
            catch (UniqueConstraintViolationException)
            {
                // The same idempotency key was committed concurrently: our transfer
                // rolled back with the failed insert, so return the committed outcome.
                var stored = await GetStoredResultAsync(command, cancellationToken);
                if (stored is not null)
                {
                    return Result.Success(stored.TransactionId);
                }

                return Result.Failure<Guid>(TransferErrors.Conflict);
            }
        }

        return Result.Failure<Guid>(TransferErrors.Conflict);
    }

    private async Task<Result<Guid>> ExecuteOnceAsync(TransferMoneyCommand command, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var transactions = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var idempotency = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var replay = await idempotency.GetAsync(command.IdempotencyKey, command.Requester, cancellationToken);
        if (replay is not null)
        {
            return Result.Success(replay.TransactionId);
        }

        var source = await accounts.GetByIdAsync(new AccountId(command.SourceAccountId), cancellationToken);
        if (source is null || source.Owner != command.Requester)
        {
            return Result.Failure<Guid>(AccountApplicationErrors.NotFound);
        }

        var destination = await accounts.GetByIdAsync(new AccountId(command.DestinationAccountId), cancellationToken);
        if (destination is null)
        {
            return Result.Failure<Guid>(AccountApplicationErrors.NotFound);
        }

        var currency = Currency.Create(command.CurrencyCode?.Trim().ToUpperInvariant() ?? string.Empty);
        if (currency.IsFailure)
        {
            return Result.Failure<Guid>(currency.Error);
        }

        var amount = Money.Create(command.Amount, currency.Value);
        if (amount.IsFailure)
        {
            return Result.Failure<Guid>(amount.Error);
        }

        var totals = await transactions.GetEntryTotalsAsync(source.Id, cancellationToken);
        var sourceBalance = LedgerMath.Balance(source, totals.Debits, totals.Credits);

        var now = timeProvider.GetUtcNow();
        var startOfDay = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var transferredToday = await transactions.GetTransferredTotalAsync(
            source.Id, startOfDay, startOfDay.AddDays(1), cancellationToken);

        var transfer = TransferPolicy.Transfer(
            source,
            sourceBalance,
            Money.Create(transferredToday, source.Currency).Value,
            destination,
            amount.Value,
            now);
        if (transfer.IsFailure)
        {
            return Result.Failure<Guid>(transfer.Error);
        }

        source.RecordMovement();
        destination.RecordMovement();
        await transactions.AddAsync(transfer.Value, cancellationToken);
        await outbox.EnqueueAsync(
            new MoneyTransferred(
                transfer.Value.Id.Value,
                source.Id.Value,
                destination.Id.Value,
                amount.Value.Amount,
                currency.Value.Code,
                now),
            cancellationToken);
        await idempotency.AddAsync(
            new IdempotencyRecord(command.IdempotencyKey, command.Requester, transfer.Value.Id.Value, now),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(transfer.Value.Id.Value);
    }

    private async Task<IdempotencyRecord?> GetStoredResultAsync(
        TransferMoneyCommand command, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var idempotency = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        return await idempotency.GetAsync(command.IdempotencyKey, command.Requester, cancellationToken);
    }
}
