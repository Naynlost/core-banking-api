using Banking.Application.Abstractions;
using Banking.Application.Common;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Application.Transactions.ReverseTransaction;

// Transferler gibi Version token'ı üzerinden çakışır; çift ters kayıt önce repository kontrolüyle,
// asıl güvence olarak da reversal linkindeki unique index ile engellenir
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
                // Başka bir hareket hesaplardan birine dokunmuş, taze state ile tekrar dene
            }
            catch (ConcurrencyConflictException)
            {
                return Result.Failure<Guid>(ReversalApplicationErrors.Conflict);
            }
            catch (UniqueConstraintViolationException)
            {
                // Aynı işlemin eş zamanlı ters kaydı unique index'i kazanmış
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

        // Hiçbir şey açığa çıkmadan önce requester'ın işlemde yer aldığı doğrulanmalı
        var owned = involved.Where(a => a.Owner == command.Requester).ToList();
        if (owned.Count == 0)
        {
            return Result.Failure<Guid>(ReversalErrors.NotFound);
        }

        if (await transactions.HasReversalAsync(original.Id, cancellationToken))
        {
            return Result.Failure<Guid>(ReversalErrors.AlreadyReversed);
        }

        // İade eden, orijinalin alacaklandırdığı hesap; sadece borçlandıysa policy net hatayla reddeder
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
    public const string Conflict = "reversal.conflict";
}
