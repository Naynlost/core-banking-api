using Banking.Application.Abstractions;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Persistence.Repositories;

internal sealed class TransactionRepository(BankingDbContext context) : ITransactionRepository
{
    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken) =>
        await context.Transactions.AddAsync(transaction, cancellationToken);

    public async Task<Transaction?> GetByIdAsync(TransactionId id, CancellationToken cancellationToken) =>
        await context.Transactions
            .Include(t => t.Entries)
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<bool> HasReversalAsync(TransactionId id, CancellationToken cancellationToken) =>
        await context.Transactions.AnyAsync(t => t.ReversesTransactionId == id, cancellationToken);

    public async Task<StatementPage> GetStatementAsync(
        AccountId accountId, int skip, int take, CancellationToken cancellationToken)
    {
        var entries = context.LedgerEntries.Where(e => e.AccountId == accountId);
        var totalCount = await entries.CountAsync(cancellationToken);

        var lines = await entries
            .Join(context.Transactions, e => e.TransactionId, t => t.Id, (e, t) => new { Entry = e, t.Description })
            .OrderByDescending(x => x.Entry.OccurredAt)
            .ThenByDescending(x => x.Entry.Id)
            .Skip(skip)
            .Take(take)
            .Select(x => new
            {
                x.Entry.TransactionId,
                x.Description,
                x.Entry.Direction,
                Amount = x.Entry.Amount.Amount,
                Currency = x.Entry.Amount.Currency,
                x.Entry.OccurredAt,
            })
            .ToListAsync(cancellationToken);

        return new StatementPage(
            lines
                .Select(x => new StatementLine(
                    x.TransactionId.Value, x.Description, x.Direction, x.Amount, x.Currency.Code, x.OccurredAt))
                .ToList(),
            totalCount);
    }

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

    // Her transfer kaynağı tam bir kez borçlandırır; bu satırlar hem gönderilen tutarı hem sayıyı verir
    private IQueryable<LedgerEntry> TransferDebits(AccountId accountId, DateTimeOffset from, DateTimeOffset to) =>
        context.Transactions
            .Where(t => t.Description == TransferPolicy.TransferDescription)
            .SelectMany(t => t.Entries)
            .Where(e => e.AccountId == accountId
                && e.Direction == EntryDirection.Debit
                && e.OccurredAt >= from
                && e.OccurredAt < to);
}
