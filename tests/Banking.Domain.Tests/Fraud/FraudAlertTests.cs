using Banking.Domain.Fraud;
using Banking.Domain.Ledgers;
using Shouldly;

namespace Banking.Domain.Tests.Fraud;

public class FraudAlertTests
{
    private static readonly DateTimeOffset FlaggedAt = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    private static FraudAlert Raise() => FraudAlert.Raise(
        new TransactionId(Guid.NewGuid()),
        new FraudFlag(FraudPolicy.AmountAboveThresholdRule, "detail"),
        FlaggedAt);

    [Fact]
    public void Raise_CreatesOpenAlert()
    {
        var alert = Raise();

        alert.Status.ShouldBe(FraudAlertStatus.Open);
        alert.ResolvedAt.ShouldBeNull();
        alert.ResolutionNote.ShouldBeNull();
    }

    [Theory]
    [InlineData(FraudAlertStatus.Confirmed)]
    [InlineData(FraudAlertStatus.Dismissed)]
    public void Resolve_OpenAlert_RecordsVerdictNoteAndTimestamp(FraudAlertStatus resolution)
    {
        var alert = Raise();
        var resolvedAt = FlaggedAt.AddHours(1);

        var result = alert.Resolve(resolution, "  reviewed by ops  ", resolvedAt);

        result.IsSuccess.ShouldBeTrue();
        alert.Status.ShouldBe(resolution);
        alert.ResolvedAt.ShouldBe(resolvedAt);
        alert.ResolutionNote.ShouldBe("reviewed by ops");
    }

    [Fact]
    public void Resolve_WithoutNote_LeavesNoteEmpty()
    {
        var alert = Raise();

        alert.Resolve(FraudAlertStatus.Dismissed, "   ", FlaggedAt.AddHours(1)).IsSuccess.ShouldBeTrue();

        alert.ResolutionNote.ShouldBeNull();
    }

    [Fact]
    public void Resolve_Twice_FailsAndKeepsTheFirstVerdict()
    {
        var alert = Raise();
        alert.Resolve(FraudAlertStatus.Confirmed, "first", FlaggedAt.AddHours(1)).IsSuccess.ShouldBeTrue();

        var second = alert.Resolve(FraudAlertStatus.Dismissed, "second", FlaggedAt.AddHours(2));

        second.IsFailure.ShouldBeTrue();
        second.Error.ShouldBe(FraudAlertErrors.AlreadyResolved);
        alert.Status.ShouldBe(FraudAlertStatus.Confirmed);
        alert.ResolutionNote.ShouldBe("first");
    }

    [Fact]
    public void Resolve_BackToOpen_IsRejected()
    {
        var alert = Raise();

        var result = alert.Resolve(FraudAlertStatus.Open, null, FlaggedAt.AddHours(1));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(FraudAlertErrors.InvalidResolution);
        alert.Status.ShouldBe(FraudAlertStatus.Open);
    }
}
