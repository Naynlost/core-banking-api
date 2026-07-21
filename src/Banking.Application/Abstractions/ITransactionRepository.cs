using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;

namespace Banking.Application.Abstractions;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken);

    Task<Transaction?> GetByIdAsync(TransactionId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(AccountId accountId, CancellationToken cancellationToken);

    Task<bool> HasReversalAsync(TransactionId id, CancellationToken cancellationToken);

    // En yeni kayıt önce
    Task<StatementPage> GetStatementAsync(AccountId accountId, int skip, int take, CancellationToken cancellationToken);

    Task<EntryTotals> GetEntryTotalsAsync(AccountId accountId, CancellationToken cancellationToken);

    // Sadece transfer işlemlerinin borç bacağını sayar
    Task<decimal> GetTransferredTotalAsync(
        AccountId accountId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task<int> CountTransfersAsync(
        AccountId accountId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

public readonly record struct EntryTotals(decimal Debits, decimal Credits);

public sealed record StatementLine(
    Guid TransactionId,
    string Description,
    EntryDirection Direction,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset OccurredAt);

public sealed record StatementPage(IReadOnlyList<StatementLine> Lines, int TotalCount);
