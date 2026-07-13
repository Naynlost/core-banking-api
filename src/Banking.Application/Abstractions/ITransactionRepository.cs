using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;

namespace Banking.Application.Abstractions;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken);

    /// <summary>All transactions that touch the given account, entries included.</summary>
    Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(AccountId accountId, CancellationToken cancellationToken);

    /// <summary>Total debits and credits posted against the account; input for balance derivation.</summary>
    Task<EntryTotals> GetEntryTotalsAsync(AccountId accountId, CancellationToken cancellationToken);

    /// <summary>Total the account sent by transfer (debit legs of transfer transactions) in [from, to).</summary>
    Task<decimal> GetTransferredTotalAsync(
        AccountId accountId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    /// <summary>Number of transfer transactions that debited the account in [from, to).</summary>
    Task<int> CountTransfersAsync(
        AccountId accountId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

public readonly record struct EntryTotals(decimal Debits, decimal Credits);
