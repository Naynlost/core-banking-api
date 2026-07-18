using Banking.Application.Abstractions;
using Banking.Domain.Accounts;
using Banking.Domain.Fraud;
using Banking.Domain.Ledgers;
using Banking.Domain.StandingOrders;
using Banking.Domain.ValueObjects;

namespace Banking.Application.Tests.Fakes;

internal sealed class InMemoryBalanceProjection : IBalanceProjection
{
    private readonly Dictionary<AccountId, EntryTotals> _totals = [];

    public void SetTotals(AccountId accountId, decimal debits, decimal credits) =>
        _totals[accountId] = new EntryTotals(debits, credits);

    public Task<EntryTotals> GetTotalsAsync(AccountId accountId, CancellationToken cancellationToken) =>
        Task.FromResult(_totals.GetValueOrDefault(accountId));
}

internal sealed class InMemoryStandingOrderRepository : IStandingOrderRepository
{
    private readonly List<StandingOrder> _orders = [];

    public IReadOnlyList<StandingOrder> Orders => _orders;

    public Task AddAsync(StandingOrder order, CancellationToken cancellationToken)
    {
        _orders.Add(order);
        return Task.CompletedTask;
    }

    public Task<StandingOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_orders.FirstOrDefault(order => order.Id == id));

    public Task<IReadOnlyList<StandingOrder>> GetByOwnerAsync(string owner, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StandingOrder>>(_orders.Where(order => order.Owner == owner).ToList());

    public Task<IReadOnlyList<StandingOrder>> GetDueAsync(
        DateTimeOffset now, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StandingOrder>>(_orders
            .Where(order => order.IsActive && order.NextRunAt <= now)
            .OrderBy(order => order.NextRunAt)
            .Take(take)
            .ToList());
}

internal sealed class InMemoryFraudAlertRepository : IFraudAlertRepository
{
    private readonly List<FraudAlert> _alerts = [];

    public void Seed(FraudAlert alert) => _alerts.Add(alert);

    public Task<FraudAlertPage> ListAsync(
        FraudAlertStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        var filtered = _alerts
            .Where(alert => status is null || alert.Status == status)
            .OrderByDescending(alert => alert.FlaggedAt)
            .ThenBy(alert => alert.Id)
            .ToList();

        return Task.FromResult(new FraudAlertPage(
            filtered.Skip(skip).Take(take).ToList(), filtered.Count));
    }

    public Task<FraudAlert?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_alerts.FirstOrDefault(alert => alert.Id == id));
}

internal sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly Dictionary<AccountId, Account> _accounts = [];

    public IReadOnlyCollection<Account> Accounts => _accounts.Values;

    public Task<Account?> GetByIdAsync(AccountId id, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.GetValueOrDefault(id));

    public Task<IReadOnlyList<Account>> GetByOwnerAsync(string owner, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Account>>(_accounts.Values.Where(a => a.Owner == owner).ToList());

    public Task<Account?> GetCashAccountAsync(Currency currency, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.Values.FirstOrDefault(a =>
            a.Owner == Account.SystemOwner && a.Type == AccountType.Asset && a.Currency == currency));

    public Task AddAsync(Account account, CancellationToken cancellationToken)
    {
        _accounts[account.Id] = account;
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly Dictionary<AccountId, EntryTotals> _totals = [];
    private decimal _transferredToday;

    public List<Transaction> Added { get; } = [];

    public void SetTotals(AccountId accountId, decimal debits, decimal credits) =>
        _totals[accountId] = new EntryTotals(debits, credits);

    public void SetTransferredToday(decimal amount) => _transferredToday = amount;

    public Task AddAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        Added.Add(transaction);
        return Task.CompletedTask;
    }

    public Task<Transaction?> GetByIdAsync(TransactionId id, CancellationToken cancellationToken) =>
        Task.FromResult(Added.FirstOrDefault(t => t.Id == id));

    public Task<bool> HasReversalAsync(TransactionId id, CancellationToken cancellationToken) =>
        Task.FromResult(Added.Any(t => t.ReversesTransactionId == id));

    public Task<StatementPage> GetStatementAsync(
        AccountId accountId, int skip, int take, CancellationToken cancellationToken)
    {
        var lines = Added
            .SelectMany(t => t.Entries.Select(e => (Transaction: t, Entry: e)))
            .Where(x => x.Entry.AccountId == accountId)
            .OrderByDescending(x => x.Entry.OccurredAt)
            .ToList();

        return Task.FromResult(new StatementPage(
            lines
                .Skip(skip)
                .Take(take)
                .Select(x => new StatementLine(
                    x.Transaction.Id.Value,
                    x.Transaction.Description,
                    x.Entry.Direction,
                    x.Entry.Amount.Amount,
                    x.Entry.Amount.Currency.Code,
                    x.Entry.OccurredAt))
                .ToList(),
            lines.Count));
    }

    public Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(
        AccountId accountId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Transaction>>(
            Added.Where(t => t.Entries.Any(e => e.AccountId == accountId)).ToList());

    public Task<EntryTotals> GetEntryTotalsAsync(AccountId accountId, CancellationToken cancellationToken) =>
        Task.FromResult(_totals.GetValueOrDefault(accountId));

    public Task<decimal> GetTransferredTotalAsync(
        AccountId accountId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult(_transferredToday);

    public Task<int> CountTransfersAsync(
        AccountId accountId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult(Added.Count(t =>
            t.Entries.Any(e => e.AccountId == accountId && e.Direction == EntryDirection.Debit)));
}

/// <summary>
/// Mimics the transactional coupling between the idempotency table and the unit
/// of work: staged records become visible only after a successful save, and are
/// discarded when the save fails — exactly like a database rollback.
/// </summary>
internal sealed class StagingIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<(string Key, string UserId), IdempotencyRecord> _committed = [];
    private readonly List<IdempotencyRecord> _pending = [];

    public IReadOnlyCollection<IdempotencyRecord> Committed => _committed.Values;

    public void SeedCommitted(IdempotencyRecord record) => _committed[(record.Key, record.UserId)] = record;

    public Task<IdempotencyRecord?> GetAsync(string key, string userId, CancellationToken cancellationToken) =>
        Task.FromResult(_committed.GetValueOrDefault((key, userId)));

    public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        _pending.Add(record);
        return Task.CompletedTask;
    }

    public void CommitPending()
    {
        foreach (var record in _pending)
        {
            _committed[(record.Key, record.UserId)] = record;
        }

        _pending.Clear();
    }

    public void DiscardPending() => _pending.Clear();
}

internal sealed class InMemoryOutbox : IOutbox
{
    public List<object> Enqueued { get; } = [];

    public Task EnqueueAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : class
    {
        Enqueued.Add(@event);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork(StagingIdempotencyStore? idempotencyStore = null) : IUnitOfWork
{
    /// <summary>Exceptions to throw on successive saves before finally succeeding.</summary>
    public Queue<Exception> PendingFailures { get; } = new();

    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;

        if (PendingFailures.Count > 0)
        {
            idempotencyStore?.DiscardPending();
            throw PendingFailures.Dequeue();
        }

        idempotencyStore?.CommitPending();
        return Task.CompletedTask;
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
