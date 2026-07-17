using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using FluentValidation;

namespace Banking.Application.Transfers;

/// <summary>
/// Cheap shape checks before the handler spends database round-trips. The error
/// codes mirror what the domain would answer for the same input, so rejecting
/// early does not change the API's behavior.
/// </summary>
internal sealed class TransferMoneyCommandValidator : AbstractValidator<TransferMoneyCommand>
{
    public TransferMoneyCommandValidator()
    {
        RuleFor(c => c.IdempotencyKey)
            .NotEmpty().WithErrorCode("transfer.idempotency_key_required")
            .MaximumLength(128).WithErrorCode("transfer.idempotency_key_too_long");

        RuleFor(c => c.Amount)
            .GreaterThanOrEqualTo(0).WithErrorCode(MoneyErrors.NegativeAmount)
            .GreaterThan(0).WithErrorCode(LedgerErrors.AmountMustBePositive);

        RuleFor(c => c.CurrencyCode)
            .NotEmpty().WithErrorCode(CurrencyErrors.InvalidCode);

        RuleFor(c => c.DestinationAccountId)
            .NotEqual(c => c.SourceAccountId).WithErrorCode(LedgerErrors.SameAccount);
    }
}
