using Banking.Domain.Accounts;

namespace Banking.Application.Abstractions;

// O(1) okuma modeli; ledger yazımıyla aynı transaction'da güncellenir, gerçek kaynak yine ledger'dır
public interface IBalanceProjection
{
    Task<EntryTotals> GetTotalsAsync(AccountId accountId, CancellationToken cancellationToken);
}
