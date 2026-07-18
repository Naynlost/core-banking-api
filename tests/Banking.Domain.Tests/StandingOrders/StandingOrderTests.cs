using Banking.Domain.Accounts;
using Banking.Domain.StandingOrders;
using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Domain.Tests.StandingOrders;

public class StandingOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Money Amount = Money.Create(100m, Currency.Try).Value;

    private static StandingOrder Create(
        StandingOrderFrequency frequency = StandingOrderFrequency.Monthly,
        DateTimeOffset? firstRunAt = null) =>
        StandingOrder.Create(
            "user-1", AccountId.New(), AccountId.New(), Amount, frequency, firstRunAt ?? Now, Now).Value;

    [Fact]
    public void Create_StartsActiveWithTheGivenSchedule()
    {
        var order = Create(firstRunAt: Now.AddDays(3));

        order.Status.ShouldBe(StandingOrderStatus.Active);
        order.NextRunAt.ShouldBe(Now.AddDays(3));
        order.LastRunAt.ShouldBeNull();
    }

    [Fact]
    public void Create_ToTheSameAccount_Fails()
    {
        var account = AccountId.New();

        var result = StandingOrder.Create(
            "user-1", account, account, Amount, StandingOrderFrequency.Daily, Now, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(StandingOrderErrors.SameAccount);
    }

    [Fact]
    public void Create_WithZeroAmount_Fails()
    {
        var result = StandingOrder.Create(
            "user-1", AccountId.New(), AccountId.New(), Money.Zero(Currency.Try),
            StandingOrderFrequency.Daily, Now, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(StandingOrderErrors.AmountMustBePositive);
    }

    [Theory]
    [InlineData(StandingOrderFrequency.Daily, 1)]
    [InlineData(StandingOrderFrequency.Weekly, 7)]
    public void RecordRun_AdvancesTheScheduleFromTheScheduledTime(StandingOrderFrequency frequency, int days)
    {
        var order = Create(frequency);

        // The executor ran late; the schedule still advances from the planned time.
        order.RecordRun(Now.AddHours(5), error: null);

        order.NextRunAt.ShouldBe(Now.AddDays(days));
        order.LastRunAt.ShouldBe(Now.AddHours(5));
        order.LastRunError.ShouldBeNull();
    }

    [Fact]
    public void RecordRun_Monthly_AdvancesOneMonth()
    {
        var order = Create(StandingOrderFrequency.Monthly);

        order.RecordRun(Now, error: null);

        order.NextRunAt.ShouldBe(Now.AddMonths(1));
    }

    [Fact]
    public void RecordRun_WithError_KeepsTheFailureVisible()
    {
        var order = Create(StandingOrderFrequency.Daily);

        order.RecordRun(Now, "ledger.insufficient_funds");

        order.LastRunError.ShouldBe("ledger.insufficient_funds");
        order.NextRunAt.ShouldBe(Now.AddDays(1)); // a missed occurrence is not retried forever
    }

    [Fact]
    public void CurrentRunKey_IsStablePerOccurrenceAndChangesAfterARun()
    {
        var order = Create(StandingOrderFrequency.Daily);

        var keyBefore = order.CurrentRunKey;
        order.CurrentRunKey.ShouldBe(keyBefore); // deterministic, not random

        order.RecordRun(Now, error: null);

        order.CurrentRunKey.ShouldNotBe(keyBefore);
    }

    [Fact]
    public void Cancel_ActiveOrder_StopsIt_AndCancellingTwiceFails()
    {
        var order = Create();

        order.Cancel().IsSuccess.ShouldBeTrue();
        order.Status.ShouldBe(StandingOrderStatus.Cancelled);

        var second = order.Cancel();
        second.IsFailure.ShouldBeTrue();
        second.Error.ShouldBe(StandingOrderErrors.AlreadyCancelled);
    }
}
