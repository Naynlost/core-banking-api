using Banking.Application.Fraud;
using Banking.Application.Fraud.ListFraudAlerts;
using Banking.Application.Fraud.ResolveFraudAlert;
using Banking.Application.Tests.Fakes;
using Banking.Domain.Fraud;
using Banking.Domain.Ledgers;
using Shouldly;

namespace Banking.Application.Tests.Fraud;

public class FraudReviewHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryFraudAlertRepository _alerts = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private static FraudAlert RaiseAlert(DateTimeOffset flaggedAt) => FraudAlert.Raise(
        new TransactionId(Guid.NewGuid()),
        new FraudFlag(FraudPolicy.AmountAboveThresholdRule, "detail"),
        flaggedAt);

    [Fact]
    public async Task List_FiltersByStatusAndOrdersNewestFirst()
    {
        var older = RaiseAlert(Now.AddMinutes(-10));
        var newer = RaiseAlert(Now);
        var resolved = RaiseAlert(Now.AddMinutes(-5));
        resolved.Resolve(FraudAlertStatus.Dismissed, null, Now);
        _alerts.Seed(older);
        _alerts.Seed(newer);
        _alerts.Seed(resolved);

        var handler = new ListFraudAlertsQueryHandler(_alerts);
        var result = await handler.HandleAsync(new ListFraudAlertsQuery("open"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(2);
        result.Value.Items.Select(item => item.Id).ShouldBe([newer.Id, older.Id]);
        result.Value.Items.ShouldAllBe(item => item.Status == "Open");
    }

    [Fact]
    public async Task Resolve_OpenAlert_PersistsTheVerdict()
    {
        var alert = RaiseAlert(Now);
        _alerts.Seed(alert);
        var handler = new ResolveFraudAlertCommandHandler(_alerts, _unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(
            new ResolveFraudAlertCommand(alert.Id, "confirmed", "verified with the customer"),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        alert.Status.ShouldBe(FraudAlertStatus.Confirmed);
        alert.ResolvedAt.ShouldBe(Now);
        _unitOfWork.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Resolve_UnknownAlert_ReturnsNotFound()
    {
        var handler = new ResolveFraudAlertCommandHandler(_alerts, _unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(
            new ResolveFraudAlertCommand(Guid.NewGuid(), "Dismissed", null), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(FraudReviewErrors.NotFound);
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Resolve_AlreadyResolvedAlert_FailsWithoutSaving()
    {
        var alert = RaiseAlert(Now);
        alert.Resolve(FraudAlertStatus.Confirmed, null, Now);
        _alerts.Seed(alert);
        var handler = new ResolveFraudAlertCommandHandler(_alerts, _unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(
            new ResolveFraudAlertCommand(alert.Id, "Dismissed", null), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(FraudAlertErrors.AlreadyResolved);
        _unitOfWork.SaveCount.ShouldBe(0);
    }
}
