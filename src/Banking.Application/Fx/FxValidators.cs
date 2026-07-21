using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using FluentValidation;

namespace Banking.Application.Fx;

internal sealed class FundFxPositionCommandValidator : AbstractValidator<FundFxPositionCommand>
{
    public FundFxPositionCommandValidator()
    {
        RuleFor(c => c.IdempotencyKey)
            .NotEmpty().WithErrorCode("fx.idempotency_key_required")
            .MaximumLength(128).WithErrorCode("fx.idempotency_key_too_long");

        RuleFor(c => c.Amount)
            .GreaterThanOrEqualTo(0).WithErrorCode(MoneyErrors.NegativeAmount)
            .GreaterThan(0).WithErrorCode(LedgerErrors.AmountMustBePositive);

        RuleFor(c => c.CurrencyCode)
            .NotEmpty().WithErrorCode(CurrencyErrors.InvalidCode);
    }
}

internal sealed class GetFxQuoteQueryValidator : AbstractValidator<GetFxQuoteQuery>
{
    public GetFxQuoteQueryValidator()
    {
        RuleFor(q => q.From).NotEmpty().WithErrorCode(CurrencyErrors.InvalidCode);
        RuleFor(q => q.To).NotEmpty().WithErrorCode(CurrencyErrors.InvalidCode);

        RuleFor(q => q.Amount)
            .GreaterThan(0).WithErrorCode(LedgerErrors.AmountMustBePositive);
    }
}
