using Banking.Application.Abstractions;
using Banking.Application.Accounts;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.StandingOrders;
using Banking.Domain.ValueObjects;

namespace Banking.Application.StandingOrders;

internal sealed class CreateStandingOrderCommandHandler(
    IAccountRepository accounts,
    IStandingOrderRepository standingOrders,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateStandingOrderCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateStandingOrderCommand command, CancellationToken cancellationToken)
    {
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

        // Catch the obvious mismatch now; execution-time transfer rules recheck it.
        if (currency.Value != source.Currency)
        {
            return Result.Failure<Guid>(MoneyErrors.CurrencyMismatch);
        }

        var amount = Money.Create(command.Amount, currency.Value);
        if (amount.IsFailure)
        {
            return Result.Failure<Guid>(amount.Error);
        }

        // The validator already rejected unparseable frequencies.
        var frequency = Enum.Parse<StandingOrderFrequency>(command.Frequency, ignoreCase: true);

        var now = timeProvider.GetUtcNow();
        var order = StandingOrder.Create(
            command.Requester,
            source.Id,
            destination.Id,
            amount.Value,
            frequency,
            command.FirstRunAt ?? now,
            now);
        if (order.IsFailure)
        {
            return Result.Failure<Guid>(order.Error);
        }

        await standingOrders.AddAsync(order.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(order.Value.Id);
    }
}
