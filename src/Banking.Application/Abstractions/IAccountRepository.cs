using Banking.Domain.Accounts;

namespace Banking.Application.Abstractions;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(AccountId id, CancellationToken cancellationToken);

    Task AddAsync(Account account, CancellationToken cancellationToken);
}
