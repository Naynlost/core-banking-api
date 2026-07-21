using Banking.Application.Abstractions;
using Banking.Application.Accounts;
using Banking.Application.Common;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Events;
using Banking.Domain.Ledgers;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Application.Transfers;

// Ledger append-only olduğundan veri çakışmaz, çakışma iki tarafın da Version'ı artırmasından gelir
internal sealed class TransferMoneyCommandHandler(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : ICommandHandler<TransferMoneyCommand, Guid>
{
    internal const int MaxAttempts = IdempotentMovement.MaxAttempts;

    public async Task<Result<Guid>> HandleAsync(TransferMoneyCommand command, CancellationToken cancellationToken)
    {
        var result = await IdempotentMovement.ExecuteAsync(
            scopeFactory,
            command.IdempotencyKey,
            command.Requester,
            TransferErrors.Conflict,
            (services, ct) => AttemptAsync(services, command, ct),
            cancellationToken);

        BankingDiagnostics.Transfers.Add(
            1, new KeyValuePair<string, object?>("outcome", result.IsSuccess ? "success" : result.Error));
        return result;
    }

    private async Task<Result<Guid>> AttemptAsync(
        IServiceProvider services, TransferMoneyCommand command, CancellationToken cancellationToken)
    {
        var accounts = services.GetRequiredService<IAccountRepository>();
        var transactions = services.GetRequiredService<ITransactionRepository>();
        var idempotency = services.GetRequiredService<IIdempotencyStore>();
        var outbox = services.GetRequiredService<IOutbox>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();

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
}
