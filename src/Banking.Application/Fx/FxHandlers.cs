using Banking.Application.Abstractions;
using Banking.Application.Common;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Application.Fx;

internal sealed class FundFxPositionCommandHandler(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : ICommandHandler<FundFxPositionCommand, Guid>
{
    public Task<Result<Guid>> HandleAsync(FundFxPositionCommand command, CancellationToken cancellationToken) =>
        IdempotentMovement.ExecuteAsync(
            scopeFactory,
            command.IdempotencyKey,
            command.Requester,
            FxApplicationErrors.Conflict,
            (services, ct) => AttemptAsync(services, command, ct),
            cancellationToken);

    private async Task<Result<Guid>> AttemptAsync(
        IServiceProvider services, FundFxPositionCommand command, CancellationToken cancellationToken)
    {
        var accounts = services.GetRequiredService<IAccountRepository>();
        var transactions = services.GetRequiredService<ITransactionRepository>();
        var idempotency = services.GetRequiredService<IIdempotencyStore>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();

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

        var cash = await accounts.GetCashAccountAsync(currency.Value, cancellationToken);
        if (cash is null)
        {
            cash = Account.OpenCash(currency.Value);
            await accounts.AddAsync(cash, cancellationToken);
        }

        var position = await accounts.GetFxPositionAccountAsync(currency.Value, cancellationToken);
        if (position is null)
        {
            position = Account.OpenFxPosition(currency.Value);
            await accounts.AddAsync(position, cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        var funding = FxTreasuryPolicy.Fund(cash, position, amount.Value, now);
        if (funding.IsFailure)
        {
            return Result.Failure<Guid>(funding.Error);
        }

        cash.RecordMovement();
        position.RecordMovement();
        await transactions.AddAsync(funding.Value, cancellationToken);
        await idempotency.AddAsync(
            new IdempotencyRecord(command.IdempotencyKey, command.Requester, funding.Value.Id.Value, now),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(funding.Value.Id.Value);
    }
}

internal sealed class GetFxQuoteQueryHandler(IExchangeRateProvider rates)
    : IQueryHandler<GetFxQuoteQuery, FxQuoteResponse>
{
    public async Task<Result<FxQuoteResponse>> HandleAsync(
        GetFxQuoteQuery query, CancellationToken cancellationToken)
    {
        var from = Currency.Create(query.From?.Trim().ToUpperInvariant() ?? string.Empty);
        if (from.IsFailure)
        {
            return Result.Failure<FxQuoteResponse>(from.Error);
        }

        var to = Currency.Create(query.To?.Trim().ToUpperInvariant() ?? string.Empty);
        if (to.IsFailure)
        {
            return Result.Failure<FxQuoteResponse>(to.Error);
        }

        var amount = Money.Create(query.Amount, from.Value);
        if (amount.IsFailure)
        {
            return Result.Failure<FxQuoteResponse>(amount.Error);
        }

        var rate = await rates.GetRateAsync(from.Value, to.Value, cancellationToken);
        if (rate.IsFailure)
        {
            return Result.Failure<FxQuoteResponse>(rate.Error);
        }

        var converted = rate.Value.Convert(amount.Value);
        if (converted.IsFailure)
        {
            return Result.Failure<FxQuoteResponse>(converted.Error);
        }

        return Result.Success(new FxQuoteResponse(
            from.Value.Code,
            to.Value.Code,
            rate.Value.Rate,
            amount.Value.Amount,
            converted.Value.Amount));
    }
}
