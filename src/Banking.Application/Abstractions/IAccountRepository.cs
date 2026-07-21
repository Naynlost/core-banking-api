using Banking.Domain.Accounts;
using Banking.Domain.ValueObjects;

namespace Banking.Application.Abstractions;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(AccountId id, CancellationToken cancellationToken);

    // En eski hesap önce döner
    Task<IReadOnlyList<Account>> GetByOwnerAsync(string owner, CancellationToken cancellationToken);

    Task<Account?> GetCashAccountAsync(Currency currency, CancellationToken cancellationToken);

    // Bankanın o para birimindeki döviz pozisyonu; hiç kullanılmamışsa null
    Task<Account?> GetFxPositionAccountAsync(Currency currency, CancellationToken cancellationToken);

    Task AddAsync(Account account, CancellationToken cancellationToken);
}
