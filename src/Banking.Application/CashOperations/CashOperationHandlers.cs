using Banking.Application.Abstractions;
using Banking.Application.Accounts;
using Banking.Application.Common;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Application.CashOperations;

internal sealed class DepositMoneyCommandHandler(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : ICommandHandler<DepositMoneyCommand, Guid>
{
    public Task<Result<Guid>> HandleAsync(DepositMoneyCommand command, CancellationToken cancellationToken) =>
        CashMovement.ExecuteAsync(
            scopeFactory, timeProvider, "deposit", command.IdempotencyKey, command.Requester,
            command.AccountId, command.Amount, command.CurrencyCode, cancellationToken);
}

internal sealed class WithdrawMoneyCommandHandler(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : ICommandHandler<WithdrawMoneyCommand, Guid>
{
    public Task<Result<Guid>> HandleAsync(WithdrawMoneyCommand command, CancellationToken cancellationToken) =>
        CashMovement.ExecuteAsync(
            scopeFactory, timeProvider, "withdrawal", command.IdempotencyKey, command.Requester,
            command.AccountId, command.Amount, command.CurrencyCode, cancellationToken);
}

// Yatırma/çekme sadece yön bakımından farklı, ikisi de bu ortak çekirdeği kullanır
internal static class CashMovement
{
    public static async Task<Result<Guid>> ExecuteAsync(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        string kind,
        string idempotencyKey,
        string requester,
        Guid accountId,
        decimal commandAmount,
        string? currencyCode,
        CancellationToken cancellationToken)
    {
        var result = await IdempotentMovement.ExecuteAsync(
            scopeFactory,
            idempotencyKey,
            requester,
            CashOperationErrors.Conflict,
            (services, ct) => AttemptAsync(
                services, timeProvider, kind, idempotencyKey, requester, accountId, commandAmount, currencyCode, ct),
            cancellationToken);

        BankingDiagnostics.CashOperations.Add(
            1,
            new KeyValuePair<string, object?>("kind", kind),
            new KeyValuePair<string, object?>("outcome", result.IsSuccess ? "success" : result.Error));
        return result;
    }

    private static async Task<Result<Guid>> AttemptAsync(
        IServiceProvider services,
        TimeProvider timeProvider,
        string kind,
        string idempotencyKey,
        string requester,
        Guid accountId,
        decimal commandAmount,
        string? currencyCode,
        CancellationToken cancellationToken)
    {
        var accounts = services.GetRequiredService<IAccountRepository>();
        var transactions = services.GetRequiredService<ITransactionRepository>();
        var idempotency = services.GetRequiredService<IIdempotencyStore>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();

        var account = await accounts.GetByIdAsync(new AccountId(accountId), cancellationToken);
        if (account is null || account.Owner != requester)
        {
            return Result.Failure<Guid>(AccountApplicationErrors.NotFound);
        }

        var currency = Currency.Create(currencyCode?.Trim().ToUpperInvariant() ?? string.Empty);
        if (currency.IsFailure)
        {
            return Result.Failure<Guid>(currency.Error);
        }

        var amount = Money.Create(commandAmount, currency.Value);
        if (amount.IsFailure)
        {
            return Result.Failure<Guid>(amount.Error);
        }

        var cash = await accounts.GetCashAccountAsync(currency.Value, cancellationToken);
        if (cash is null)
        {
            // Eş zamanlı ilk hareket ikinci bir kasa hesabı oluşturabilir, ikisi de bankaya ait olduğundan sorun olmaz
            cash = Account.OpenCash(currency.Value);
            await accounts.AddAsync(cash, cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        Result<Transaction> movement;
        if (kind == "deposit")
        {
            movement = CashPolicy.Deposit(account, cash, amount.Value, now);
        }
        else
        {
            var totals = await transactions.GetEntryTotalsAsync(account.Id, cancellationToken);
            var balance = LedgerMath.Balance(account, totals.Debits, totals.Credits);
            movement = CashPolicy.Withdraw(account, cash, balance, amount.Value, now);
        }

        if (movement.IsFailure)
        {
            return Result.Failure<Guid>(movement.Error);
        }

        account.RecordMovement();
        cash.RecordMovement();
        await transactions.AddAsync(movement.Value, cancellationToken);
        await idempotency.AddAsync(
            new IdempotencyRecord(idempotencyKey, requester, movement.Value.Id.Value, now),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(movement.Value.Id.Value);
    }
}
