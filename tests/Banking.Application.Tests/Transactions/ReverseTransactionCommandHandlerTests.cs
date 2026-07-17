using Banking.Application.Abstractions;
using Banking.Application.Tests.Fakes;
using Banking.Application.Transactions.ReverseTransaction;
using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Application.Tests.Transactions;

public class ReverseTransactionCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryTransactionRepository _transactions = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Account _source;
    private readonly Account _destination;
    private readonly Transaction _transfer;
    private readonly IServiceScopeFactory _scopeFactory;

    public ReverseTransactionCommandHandlerTests()
    {
        _source = Account.Open("user-1", Currency.Try).Value;
        _destination = Account.Open("user-2", Currency.Try).Value;
        _accounts.AddAsync(_source, CancellationToken.None);
        _accounts.AddAsync(_destination, CancellationToken.None);

        var amount = Money.Create(40m, Currency.Try).Value;
        _transfer = Transaction.Create(
            TransferPolicy.TransferDescription,
            Now.AddMinutes(-5),
            [
                new EntrySpec(_source.Id, amount, EntryDirection.Debit),
                new EntrySpec(_destination.Id, amount, EntryDirection.Credit),
            ]).Value;
        _transactions.Added.Add(_transfer);
        _transactions.SetTotals(_destination.Id, debits: 0, credits: 40m); // the receiver still holds the money

        var services = new ServiceCollection();
        services.AddSingleton<IAccountRepository>(_accounts);
        services.AddSingleton<ITransactionRepository>(_transactions);
        services.AddSingleton<IUnitOfWork>(_unitOfWork);
        _scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private ReverseTransactionCommandHandler Handler => new(_scopeFactory, new FixedTimeProvider(Now));

    [Fact]
    public async Task Handle_ByTheReceiver_PostsTheReversal()
    {
        var result = await Handler.HandleAsync(
            new ReverseTransactionCommand(_transfer.Id.Value, "user-2"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var reversal = _transactions.Added.Single(t => t.Id.Value == result.Value);
        reversal.ReversesTransactionId.ShouldBe(_transfer.Id);
        reversal.Entries.ShouldContain(e => e.AccountId == _destination.Id && e.Direction == EntryDirection.Debit);
        reversal.Entries.ShouldContain(e => e.AccountId == _source.Id && e.Direction == EntryDirection.Credit);
        _source.Version.ShouldBe(1);
        _destination.Version.ShouldBe(1);
        _unitOfWork.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_WhenAlreadyReversed_Fails()
    {
        (await Handler.HandleAsync(
            new ReverseTransactionCommand(_transfer.Id.Value, "user-2"), CancellationToken.None))
            .IsSuccess.ShouldBeTrue();

        var second = await Handler.HandleAsync(
            new ReverseTransactionCommand(_transfer.Id.Value, "user-2"), CancellationToken.None);

        second.IsFailure.ShouldBeTrue();
        second.Error.ShouldBe(ReversalErrors.AlreadyReversed);
    }

    [Fact]
    public async Task Handle_ByAnUninvolvedUser_ReturnsNotFoundToHideExistence()
    {
        var result = await Handler.HandleAsync(
            new ReverseTransactionCommand(_transfer.Id.Value, "user-3"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ReversalErrors.NotFound);
        _transactions.Added.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_ByTheSender_FailsBecauseOnlyTheReceiverGivesMoneyBack()
    {
        var result = await Handler.HandleAsync(
            new ReverseTransactionCommand(_transfer.Id.Value, "user-1"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ReversalErrors.OnlyCreditedAccountCanReverse);
    }

    [Fact]
    public async Task Handle_ForUnknownTransaction_ReturnsNotFound()
    {
        var result = await Handler.HandleAsync(
            new ReverseTransactionCommand(Guid.NewGuid(), "user-1"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ReversalErrors.NotFound);
    }
}
