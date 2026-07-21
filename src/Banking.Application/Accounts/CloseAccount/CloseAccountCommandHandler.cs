using Banking.Application.Abstractions;
using Banking.Application.Messaging;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.Primitives;

namespace Banking.Application.Accounts.CloseAccount;

internal sealed class CloseAccountCommandHandler(
    IAccountRepository accounts,
    ITransactionRepository transactions,
    IUnitOfWork unitOfWork) : ICommandHandler<CloseAccountCommand>
{
    public async Task<Result> HandleAsync(CloseAccountCommand command, CancellationToken cancellationToken)
    {
        var account = await accounts.GetByIdAsync(new AccountId(command.AccountId), cancellationToken);
        if (account is null || account.Owner != command.Requester)
        {
            return Result.Failure(AccountApplicationErrors.NotFound);
        }

        var totals = await transactions.GetEntryTotalsAsync(account.Id, cancellationToken);
        var balance = LedgerMath.Balance(account, totals.Debits, totals.Credits);
        if (!balance.IsZero)
        {
            return Result.Failure(AccountErrors.BalanceMustBeZero);
        }

        var result = account.Close();
        if (result.IsFailure)
        {
            return result;
        }

        try
        {
            // Close, Version'ı artırır; kapanışla yarışan bir hareket sessizce kaybolmak yerine save'de çakışır
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure(AccountApplicationErrors.Conflict);
        }

        return Result.Success();
    }
}
