using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Domain.Fraud;
using Banking.Domain.Primitives;

namespace Banking.Application.Fraud.ResolveFraudAlert;

internal sealed class ResolveFraudAlertCommandHandler(
    IFraudAlertRepository alerts,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ResolveFraudAlertCommand>
{
    public async Task<Result> HandleAsync(ResolveFraudAlertCommand command, CancellationToken cancellationToken)
    {
        var alert = await alerts.GetByIdAsync(command.AlertId, cancellationToken);
        if (alert is null)
        {
            return Result.Failure(FraudReviewErrors.NotFound);
        }

        // The validator already rejected values outside Confirmed/Dismissed.
        var resolution = Enum.Parse<FraudAlertStatus>(command.Resolution, ignoreCase: true);

        var result = alert.Resolve(resolution, command.Note, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
