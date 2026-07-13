using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Application.Accounts.CreateAccount;

internal sealed class CreateAccountCommandHandler(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateAccountCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateAccountCommand command, CancellationToken cancellationToken)
    {
        var currency = Currency.Create(command.CurrencyCode?.Trim().ToUpperInvariant() ?? string.Empty);
        if (currency.IsFailure)
        {
            return Result.Failure<Guid>(currency.Error);
        }

        var account = Account.Open(command.Owner, currency.Value);
        if (account.IsFailure)
        {
            return Result.Failure<Guid>(account.Error);
        }

        await accounts.AddAsync(account.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(account.Value.Id.Value);
    }
}
