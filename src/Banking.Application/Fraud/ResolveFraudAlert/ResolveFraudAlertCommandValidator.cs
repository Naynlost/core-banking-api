using Banking.Domain.Fraud;
using FluentValidation;

namespace Banking.Application.Fraud.ResolveFraudAlert;

internal sealed class ResolveFraudAlertCommandValidator : AbstractValidator<ResolveFraudAlertCommand>
{
    public ResolveFraudAlertCommandValidator()
    {
        RuleFor(c => c.Resolution)
            .Must(resolution => Enum.TryParse<FraudAlertStatus>(resolution, ignoreCase: true, out var parsed)
                && parsed != FraudAlertStatus.Open)
            .WithErrorCode(FraudAlertErrors.InvalidResolution);

        RuleFor(c => c.Note)
            .MaximumLength(500).WithErrorCode(FraudReviewErrors.NoteTooLong);
    }
}
