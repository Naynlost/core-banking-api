using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;

namespace Banking.Application.Abstractions;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken);

    /// <summary>The transaction with its entries, or null.</summary>
    Task<Transaction?> GetByIdAsync(TransactionId id, CancellationToken cancellationToken);

    /// <summary>All transactions that touch the given account, entries included.</summary>
    Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(AccountId accountId, CancellationToken cancellationToken);

    /// <summary>Whether a reversal transaction for the given transaction was already posted.</summary>
    Task<bool> HasReversalAsync(TransactionId id, CancellationToken cancellationToken);

    /// <summary>One page of the account's statement: its ledger entries, newest first.</summary>
    Task<StatementPage> GetStatementAsync(AccountId accountId, int skip, int take, CancellationToken cancellationToken);

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

/// <summary>One statement row: how a single transaction touched the account.</summary>
public sealed record StatementLine(
    Guid TransactionId,
    string Description,
    EntryDirection Direction,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset OccurredAt);

public sealed record StatementPage(IReadOnlyList<StatementLine> Lines, int TotalCount);
