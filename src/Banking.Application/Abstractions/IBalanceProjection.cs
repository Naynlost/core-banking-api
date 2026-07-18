using Banking.Domain.Accounts;

namespace Banking.Application.Abstractions;

/// <summary>
/// O(1) read model for account balances. The ledger stays the source of truth —
/// the projection is maintained in the same database transaction as the ledger
/// write, can always be rebuilt from the entries, and is only used for reads;
/// domain rules keep deriving from the ledger itself.
/// </summary>
public interface IBalanceProjection
{
    /// <summary>Projected totals; zeros when the account has no movements yet.</summary>
    Task<EntryTotals> GetTotalsAsync(AccountId accountId, CancellationToken cancellationToken);
}
