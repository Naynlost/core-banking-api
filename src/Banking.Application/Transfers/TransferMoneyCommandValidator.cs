using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using FluentValidation;

namespace Banking.Application.Transfers;

// Handler DB'ye gitmeden önce ucuz şekil kontrolleri; hata kodları domain'inkiyle aynı
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
