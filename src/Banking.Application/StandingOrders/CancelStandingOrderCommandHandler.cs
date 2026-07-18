using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Domain.Primitives;

namespace Banking.Application.StandingOrders;

internal sealed class CancelStandingOrderCommandHandler(
    IStandingOrderRepository standingOrders,
    IUnitOfWork unitOfWork) : ICommandHandler<CancelStandingOrderCommand>
{
    public async Task<Result> HandleAsync(CancelStandingOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await standingOrders.GetByIdAsync(command.StandingOrderId, cancellationToken);
        if (order is null || order.Owner != command.Requester)
        {
            return Result.Failure(StandingOrderApplicationErrors.NotFound);
        }

        var result = order.Cancel();
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
