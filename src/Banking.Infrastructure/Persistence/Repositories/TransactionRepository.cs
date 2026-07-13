using Banking.Application.Abstractions;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Persistence.Repositories;

internal sealed class TransactionRepository(BankingDbContext context) : ITransactionRepository
{
    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken) =>
        await context.Transactions.AddAsync(transaction, cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(
        AccountId accountId,
        CancellationToken cancellationToken) =>
        await context.Transactions
            .Include(t => t.Entries)
            .Where(t => t.Entries.Any(e => e.AccountId == accountId))
            .OrderBy(t => t.OccurredAt)
            .ToListAsync(cancellationToken);

    public async Task<EntryTotals> GetEntryTotalsAsync(AccountId accountId, CancellationToken cancellationToken)
    {
        var totals = await context.LedgerEntries
            .Where(e => e.AccountId == accountId)
            .GroupBy(e => e.Direction)
            .Select(g => new { Direction = g.Key, Total = g.Sum(e => e.Amount.Amount) })
            .ToListAsync(cancellationToken);

        return new EntryTotals(
            totals.SingleOrDefault(t => t.Direction == EntryDirection.Debit)?.Total ?? 0m,
            totals.SingleOrDefault(t => t.Direction == EntryDirection.Credit)?.Total ?? 0m);
    }

    public async Task<decimal> GetTransferredTotalAsync(
        AccountId accountId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await TransferDebits(accountId, from, to).SumAsync(e => e.Amount.Amount, cancellationToken);

    public async Task<int> CountTransfersAsync(
        AccountId accountId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await TransferDebits(accountId, from, to).CountAsync(cancellationToken);

    // Every transfer debits its source exactly once, so the debit legs of
    // transfer transactions are both the amount sent and the transfer count.
    private IQueryable<LedgerEntry> TransferDebits(AccountId accountId, DateTimeOffset from, DateTimeOffset to) =>
        context.Transactions
            .Where(t => t.Description == TransferPolicy.TransferDescription)
            .SelectMany(t => t.Entries)
            .Where(e => e.AccountId == accountId
                && e.Direction == EntryDirection.Debit
                && e.OccurredAt >= from
                && e.OccurredAt < to);
}
