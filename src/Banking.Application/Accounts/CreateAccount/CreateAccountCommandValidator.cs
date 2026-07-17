using Banking.Domain.ValueObjects;
using FluentValidation;

namespace Banking.Application.Accounts.CreateAccount;

internal sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(c => c.CurrencyCode)
            .NotEmpty().WithErrorCode(CurrencyErrors.InvalidCode);
    }
}
