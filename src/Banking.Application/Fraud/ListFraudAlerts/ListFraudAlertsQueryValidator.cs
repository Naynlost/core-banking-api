using Banking.Domain.Fraud;
using FluentValidation;

namespace Banking.Application.Fraud.ListFraudAlerts;

internal sealed class ListFraudAlertsQueryValidator : AbstractValidator<ListFraudAlertsQuery>
{
    public ListFraudAlertsQueryValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1).WithErrorCode(FraudReviewErrors.PageOutOfRange);

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100).WithErrorCode(FraudReviewErrors.PageSizeOutOfRange);

        RuleFor(q => q.Status)
            .Must(status => string.IsNullOrWhiteSpace(status)
                || Enum.TryParse<FraudAlertStatus>(status, ignoreCase: true, out _))
            .WithErrorCode(FraudReviewErrors.InvalidStatusFilter);
    }
}
