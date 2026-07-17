using Banking.Application.Abstractions;
using Banking.Application.Common;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Application.Transactions.ReverseTransaction;

/// <summary>
/// Like transfers, a reversal moves money and so competes on the accounts'
/// version tokens — conflicts are retried on fresh state. Double reversal is
/// prevented twice: a fast repository check up front, and the unique index on
/// the reversal link as the authoritative guard when two reversals race.
/// </summary>
internal sealed class ReverseTransactionCommandHandler(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : ICommandHandler<ReverseTransactionCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(ReverseTransactionCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= IdempotentMovement.MaxAttempts; attempt++)
        {
            try
            {
                return await AttemptAsync(command, cancellationToken);
            }
            catch (ConcurrencyConflictException) when (attempt < IdempotentMovement.MaxAttempts)
            {
                // Another movement touched one of the accounts; retry on fresh state.
            }
            catch (ConcurrencyConflictException)
            {
                return Result.Failure<Guid>(ReversalApplicationErrors.Conflict);
            }
            catch (UniqueConstraintViolationException)
            {
                // A concurrent reversal of the same transaction won the unique index.
                return Result.Failure<Guid>(ReversalErrors.AlreadyReversed);
            }
        }

        return Result.Failure<Guid>(ReversalApplicationErrors.Conflict);
    }

    private async Task<Result<Guid>> AttemptAsync(
        ReverseTransactionCommand command, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var transactions = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var original = await transactions.GetByIdAsync(new TransactionId(command.TransactionId), cancellationToken);
        if (original is null)
        {
            return Result.Failure<Guid>(ReversalErrors.NotFound);
        }

        var involved = new List<Account>();
        foreach (var accountId in original.Entries.Select(e => e.AccountId).Distinct())
        {
            var account = await accounts.GetByIdAsync(accountId, cancellationToken);
            if (account is null)
            {
                return Result.Failure<Guid>(ReversalErrors.NotFound);
            }

            involved.Add(account);
        }

        // The requester must be involved at all before anything is revealed.
        var owned = involved.Where(a => a.Owner == command.Requester).ToList();
        if (owned.Count == 0)
        {
            return Result.Failure<Guid>(ReversalErrors.NotFound);
        }

        if (await transactions.HasReversalAsync(original.Id, cancellationToken))
        {
            return Result.Failure<Guid>(ReversalErrors.AlreadyReversed);
        }

        // The refunder is the requester's account the original credited; if the
        // requester was only debited, the policy rejects with the precise error.
        var refunder = owned.FirstOrDefault(a => original.Entries.Any(
                e => e.AccountId == a.Id && e.Direction == EntryDirection.Credit))
            ?? owned[0];

        var totals = await transactions.GetEntryTotalsAsync(refunder.Id, cancellationToken);
        var balance = LedgerMath.Balance(refunder, totals.Debits, totals.Credits);

        var reversal = ReversalPolicy.Reverse(original, refunder.Id, balance, involved, timeProvider.GetUtcNow());
        if (reversal.IsFailure)
        {
            return Result.Failure<Guid>(reversal.Error);
        }

        foreach (var account in involved)
        {
            account.RecordMovement();
        }

        await transactions.AddAsync(reversal.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(reversal.Value.Id.Value);
    }
}

public static class ReversalApplicationErrors
{
    /// <summary>Optimistic concurrency retries were exhausted; the client may retry.</summary>
    public const string Conflict = "reversal.conflict";
}
