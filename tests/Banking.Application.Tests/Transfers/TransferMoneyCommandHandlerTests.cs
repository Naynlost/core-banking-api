using Banking.Application.Abstractions;
using Banking.Application.Accounts;
using Banking.Application.Tests.Fakes;
using Banking.Application.Transfers;
using Banking.Domain.Accounts;
using Banking.Domain.Events;
using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Application.Tests.Transfers;

public class TransferMoneyCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryTransactionRepository _transactions = new();
    private readonly StagingIdempotencyStore _idempotency = new();
    private readonly InMemoryOutbox _outbox = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly Account _source;
    private readonly Account _destination;

    public TransferMoneyCommandHandlerTests()
    {
        _unitOfWork = new FakeUnitOfWork(_idempotency);
        _source = Account.Open("user-1", Currency.Try).Value;
        _source.CompleteKyc();
        _destination = Account.Open("user-2", Currency.Try).Value;
        _destination.CompleteKyc();
        _accounts.AddAsync(_source, CancellationToken.None);
        _accounts.AddAsync(_destination, CancellationToken.None);
        _transactions.SetTotals(_source.Id, debits: 0, credits: 100m); // bakiye: 100 TRY
    }

    private TransferMoneyCommandHandler BuildHandler(IIdempotencyStore? idempotencyOverride = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAccountRepository>(_accounts);
        services.AddSingleton<ITransactionRepository>(_transactions);
        services.AddSingleton(idempotencyOverride ?? (IIdempotencyStore)_idempotency);
        services.AddSingleton<IOutbox>(_outbox);
        services.AddSingleton<IUnitOfWork>(_unitOfWork);
        var provider = services.BuildServiceProvider();

        return new TransferMoneyCommandHandler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedTimeProvider(Now));
    }

    private TransferMoneyCommand Command(
        decimal amount = 40m,
        string key = "key-1",
        string requester = "user-1",
        Guid? sourceId = null,
        Guid? destinationId = null) =>
        new(key, requester, sourceId ?? _source.Id.Value, destinationId ?? _destination.Id.Value, amount, "TRY");

    [Fact]
    public async Task Handle_WithSufficientFunds_PostsBalancedTransferAndStoresIdempotencyRecord()
    {
        var result = await BuildHandler().HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var transaction = _transactions.Added.ShouldHaveSingleItem();
        transaction.Id.Value.ShouldBe(result.Value);
        transaction.Entries.Count.ShouldBe(2);
        transaction.Entries.ShouldContain(e => e.AccountId == _source.Id && e.Direction == EntryDirection.Debit);
        transaction.Entries.ShouldContain(e => e.AccountId == _destination.Id && e.Direction == EntryDirection.Credit);
        _unitOfWork.SaveCount.ShouldBe(1);

        var record = _idempotency.Committed.ShouldHaveSingleItem();
        record.TransactionId.ShouldBe(result.Value);
    }

    [Fact]
    public async Task Handle_WithSufficientFunds_EnqueuesMoneyTransferredEvent()
    {
        var result = await BuildHandler().HandleAsync(Command(), CancellationToken.None);

        var @event = _outbox.Enqueued.ShouldHaveSingleItem().ShouldBeOfType<MoneyTransferred>();
        @event.TransactionId.ShouldBe(result.Value);
        @event.SourceAccountId.ShouldBe(_source.Id.Value);
        @event.DestinationAccountId.ShouldBe(_destination.Id.Value);
        @event.Amount.ShouldBe(40m);
        @event.CurrencyCode.ShouldBe("TRY");
        @event.OccurredAt.ShouldBe(Now);
    }

    [Fact]
    public async Task Handle_BumpsBothAccountVersions()
    {
        await BuildHandler().HandleAsync(Command(), CancellationToken.None);

        // Fixture'daki CompleteKyc'den 1 + harekettten 1
        _source.Version.ShouldBe(2);
        _destination.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_WhenSourceKycIsPending_FailsWithoutSaving()
    {
        var pending = Account.Open("user-1", Currency.Try).Value; // KYC hiç tamamlanmadı
        await _accounts.AddAsync(pending, CancellationToken.None);
        _transactions.SetTotals(pending.Id, debits: 0, credits: 100m);

        var result = await BuildHandler().HandleAsync(
            Command(sourceId: pending.Id.Value), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.KycNotVerified);
        _transactions.Added.ShouldBeEmpty();
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_WhenDailyLimitWouldBeExceeded_FailsWithoutSaving()
    {
        _transactions.SetTotals(_source.Id, debits: 0, credits: 30_000m);
        _transactions.SetTransferredToday(15_000m); // limit 20.000

        var result = await BuildHandler().HandleAsync(Command(amount: 6_000m), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.DailyLimitExceeded);
        _transactions.Added.ShouldBeEmpty();
        _outbox.Enqueued.ShouldBeEmpty();
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_WithKnownIdempotencyKey_ReturnsStoredResultWithoutExecuting()
    {
        var storedTransactionId = Guid.NewGuid();
        _idempotency.SeedCommitted(new IdempotencyRecord("key-1", "user-1", storedTransactionId, Now));

        var result = await BuildHandler().HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(storedTransactionId);
        _transactions.Added.ShouldBeEmpty();
        _outbox.Enqueued.ShouldBeEmpty();
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_WithInsufficientFunds_FailsWithoutSaving()
    {
        var result = await BuildHandler().HandleAsync(Command(amount: 100.01m), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(LedgerErrors.InsufficientFunds);
        _transactions.Added.ShouldBeEmpty();
        _outbox.Enqueued.ShouldBeEmpty();
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_WhenSourceBelongsToAnotherUser_ReturnsNotFound()
    {
        var result = await BuildHandler().HandleAsync(Command(requester: "user-2"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountApplicationErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenDestinationDoesNotExist_ReturnsNotFound()
    {
        var result = await BuildHandler().HandleAsync(
            Command(destinationId: Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountApplicationErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenDestinationIsClosed_Fails()
    {
        _destination.Close();

        var result = await BuildHandler().HandleAsync(Command(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.Closed);
    }

    [Fact]
    public async Task Handle_RetriesOnConcurrencyConflictAndSucceeds()
    {
        _unitOfWork.PendingFailures.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));
        _unitOfWork.PendingFailures.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));

        var result = await BuildHandler().HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _unitOfWork.SaveCount.ShouldBe(3);
    }

    [Fact]
    public async Task Handle_FailsWithConflictWhenRetriesAreExhausted()
    {
        for (var i = 0; i < TransferMoneyCommandHandler.MaxAttempts; i++)
        {
            _unitOfWork.PendingFailures.Enqueue(new ConcurrencyConflictException(new InvalidOperationException()));
        }

        var result = await BuildHandler().HandleAsync(Command(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransferErrors.Conflict);
        _unitOfWork.SaveCount.ShouldBe(TransferMoneyCommandHandler.MaxAttempts);
        _idempotency.Committed.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_WhenSameKeyCommittedConcurrently_ReturnsTheCommittedResult()
    {
        // Unique-key yarışını kaybetmeyi simüle eder: save başarısız olur, store rakibin kaydını döner
        var competitorTransactionId = Guid.NewGuid();
        var store = new RacingIdempotencyStore(
            new IdempotencyRecord("key-1", "user-1", competitorTransactionId, Now));
        _unitOfWork.PendingFailures.Enqueue(
            new UniqueConstraintViolationException("pk_idempotency_keys", new InvalidOperationException()));

        var result = await BuildHandler(store).HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(competitorTransactionId);
    }

    private sealed class RacingIdempotencyStore(IdempotencyRecord committedByCompetitor) : IIdempotencyStore
    {
        private bool _firstCall = true;

        public Task<IdempotencyRecord?> GetAsync(string key, string userId, CancellationToken cancellationToken)
        {
            if (_firstCall)
            {
                _firstCall = false;
                return Task.FromResult<IdempotencyRecord?>(null);
            }

            return Task.FromResult<IdempotencyRecord?>(committedByCompetitor);
        }

        public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
