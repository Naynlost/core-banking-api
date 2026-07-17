using FluentValidation;

namespace Banking.Application.Accounts.GetStatement;

internal sealed class GetAccountStatementQueryValidator : AbstractValidator<GetAccountStatementQuery>
{
    public GetAccountStatementQueryValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1).WithErrorCode(StatementErrors.PageOutOfRange);

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100).WithErrorCode(StatementErrors.PageSizeOutOfRange);
    }
}
