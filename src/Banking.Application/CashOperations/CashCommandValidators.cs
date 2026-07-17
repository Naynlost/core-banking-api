using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using FluentValidation;

namespace Banking.Application.CashOperations;

internal sealed class DepositMoneyCommandValidator : AbstractValidator<DepositMoneyCommand>
{
    public DepositMoneyCommandValidator()
    {
        RuleFor(c => c.IdempotencyKey)
            .NotEmpty().WithErrorCode("cash_operation.idempotency_key_required")
            .MaximumLength(128).WithErrorCode("cash_operation.idempotency_key_too_long");

        RuleFor(c => c.Amount)
            .GreaterThanOrEqualTo(0).WithErrorCode(MoneyErrors.NegativeAmount)
            .GreaterThan(0).WithErrorCode(LedgerErrors.AmountMustBePositive);

        RuleFor(c => c.CurrencyCode)
            .NotEmpty().WithErrorCode(CurrencyErrors.InvalidCode);
    }
}

internal sealed class WithdrawMoneyCommandValidator : AbstractValidator<WithdrawMoneyCommand>
{
    public WithdrawMoneyCommandValidator()
    {
        RuleFor(c => c.IdempotencyKey)
            .NotEmpty().WithErrorCode("cash_operation.idempotency_key_required")
            .MaximumLength(128).WithErrorCode("cash_operation.idempotency_key_too_long");

        RuleFor(c => c.Amount)
            .GreaterThanOrEqualTo(0).WithErrorCode(MoneyErrors.NegativeAmount)
            .GreaterThan(0).WithErrorCode(LedgerErrors.AmountMustBePositive);

        RuleFor(c => c.CurrencyCode)
            .NotEmpty().WithErrorCode(CurrencyErrors.InvalidCode);
    }
}
