using Banking.Application.Accounts;
using Banking.Application.StandingOrders;
using Banking.Application.Tests.Fakes;
using Banking.Domain.Accounts;
using Banking.Domain.StandingOrders;
using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Application.Tests.StandingOrders;

public class StandingOrderHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryStandingOrderRepository _orders = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Account _source;
    private readonly Account _destination;

    public StandingOrderHandlerTests()
    {
        _source = Account.Open("user-1", Currency.Try).Value;
        _destination = Account.Open("user-2", Currency.Try).Value;
        _accounts.AddAsync(_source, CancellationToken.None);
        _accounts.AddAsync(_destination, CancellationToken.None);
    }

    private CreateStandingOrderCommandHandler BuildCreateHandler() =>
        new(_accounts, _orders, _unitOfWork, new FixedTimeProvider(Now));

    private CreateStandingOrderCommand ValidCommand(string requester = "user-1") => new(
        requester, _source.Id.Value, _destination.Id.Value, 100m, "TRY", "Monthly");

    [Fact]
    public async Task Create_WithValidInput_SchedulesTheFirstRunNow()
    {
        var result = await BuildCreateHandler().HandleAsync(ValidCommand(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var order = _orders.Orders.ShouldHaveSingleItem();
        order.Owner.ShouldBe("user-1");
        order.NextRunAt.ShouldBe(Now);
        order.Frequency.ShouldBe(StandingOrderFrequency.Monthly);
        _unitOfWork.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Create_OnSomeoneElsesSourceAccount_ReturnsNotFound()
    {
        var result = await BuildCreateHandler().HandleAsync(ValidCommand("user-2"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountApplicationErrors.NotFound);
        _orders.Orders.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_WithUnknownDestination_ReturnsNotFound()
    {
        var command = ValidCommand() with { DestinationAccountId = Guid.NewGuid() };

        var result = await BuildCreateHandler().HandleAsync(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountApplicationErrors.NotFound);
    }

    [Fact]
    public async Task Create_WithCurrencyOtherThanTheSourceAccounts_Fails()
    {
        var command = ValidCommand() with { CurrencyCode = "EUR" };

        var result = await BuildCreateHandler().HandleAsync(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MoneyErrors.CurrencyMismatch);
    }

    [Fact]
    public async Task Cancel_ByTheOwner_StopsTheOrder()
    {
        await BuildCreateHandler().HandleAsync(ValidCommand(), CancellationToken.None);
        var order = _orders.Orders.Single();
        var handler = new CancelStandingOrderCommandHandler(_orders, _unitOfWork);

        var result = await handler.HandleAsync(
            new CancelStandingOrderCommand(order.Id, "user-1"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        order.Status.ShouldBe(StandingOrderStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_ByAnotherUser_ReturnsNotFound()
    {
        await BuildCreateHandler().HandleAsync(ValidCommand(), CancellationToken.None);
        var order = _orders.Orders.Single();
        var handler = new CancelStandingOrderCommandHandler(_orders, _unitOfWork);

        var result = await handler.HandleAsync(
            new CancelStandingOrderCommand(order.Id, "user-2"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(StandingOrderApplicationErrors.NotFound);
        order.Status.ShouldBe(StandingOrderStatus.Active);
    }

    [Fact]
    public async Task List_ReturnsOnlyTheRequestersOrders()
    {
        await BuildCreateHandler().HandleAsync(ValidCommand(), CancellationToken.None);
        var handler = new ListStandingOrdersQueryHandler(_orders);

        var mine = await handler.HandleAsync(new ListStandingOrdersQuery("user-1"), CancellationToken.None);
        var theirs = await handler.HandleAsync(new ListStandingOrdersQuery("user-2"), CancellationToken.None);

        mine.Value.ShouldHaveSingleItem().CurrencyCode.ShouldBe("TRY");
        theirs.Value.ShouldBeEmpty();
    }
}
