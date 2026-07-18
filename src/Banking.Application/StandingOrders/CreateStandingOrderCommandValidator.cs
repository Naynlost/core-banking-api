using Banking.Domain.StandingOrders;
using Banking.Domain.ValueObjects;
using FluentValidation;

namespace Banking.Application.StandingOrders;

internal sealed class CreateStandingOrderCommandValidator : AbstractValidator<CreateStandingOrderCommand>
{
    public CreateStandingOrderCommandValidator()
    {
        RuleFor(c => c.Amount)
            .GreaterThanOrEqualTo(0).WithErrorCode(MoneyErrors.NegativeAmount)
            .GreaterThan(0).WithErrorCode(StandingOrderErrors.AmountMustBePositive);

        RuleFor(c => c.CurrencyCode)
            .NotEmpty().WithErrorCode(CurrencyErrors.InvalidCode);

        RuleFor(c => c.DestinationAccountId)
            .NotEqual(c => c.SourceAccountId).WithErrorCode(StandingOrderErrors.SameAccount);

        RuleFor(c => c.Frequency)
            .Must(frequency => Enum.TryParse<StandingOrderFrequency>(frequency, ignoreCase: true, out _))
            .WithErrorCode(StandingOrderApplicationErrors.InvalidFrequency);
    }
}
