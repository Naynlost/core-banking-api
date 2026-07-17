using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;

namespace Banking.Application.Abstractions;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(AccountId id, CancellationToken cancellationToken);

    /// <summary>All accounts owned by the given user, oldest first.</summary>
    Task<IReadOnlyList<Account>> GetByOwnerAsync(string owner, CancellationToken cancellationToken);

    /// <summary>The bank's own cash (asset) account for the currency, if one exists.</summary>
    Task<Account?> GetCashAccountAsync(Currency currency, CancellationToken cancellationToken);

    Task AddAsync(Account account, CancellationToken cancellationToken);
}
