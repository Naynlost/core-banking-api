using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Primitives;

namespace Banking.Application.Accounts.CompleteKyc;

internal sealed class CompleteKycCommandHandler(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork) : ICommandHandler<CompleteKycCommand>
{
    public async Task<Result> HandleAsync(CompleteKycCommand command, CancellationToken cancellationToken)
    {
        var account = await accounts.GetByIdAsync(new AccountId(command.AccountId), cancellationToken);
        if (account is null || account.Owner != command.Requester)
        {
            return Result.Failure(AccountApplicationErrors.NotFound);
        }

        var result = account.CompleteKyc();
        if (result.IsFailure)
        {
            return result;
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // A movement bumped the account version mid-save; the client may retry.
            return Result.Failure(AccountApplicationErrors.Conflict);
        }

        return Result.Success();
    }
}
