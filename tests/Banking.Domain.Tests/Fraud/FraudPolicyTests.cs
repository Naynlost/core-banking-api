using Banking.Domain.Fraud;
using Banking.Domain.Ledgers;
using Shouldly;

namespace Banking.Domain.Tests.Fraud;

public class FraudPolicyTests
{
    [Fact]
    public void Screen_AmountBelowThresholdAndNormalVelocity_RaisesNoFlags()
    {
        var flags = FraudPolicy.Screen(
            FraudPolicy.ReviewThreshold - 0.01m, "TRY", FraudPolicy.MaxTransfersPerWindow);

        flags.ShouldBeEmpty();
    }

    [Fact]
    public void Screen_AmountAtTheThreshold_FlagsForReview()
    {
        var flags = FraudPolicy.Screen(FraudPolicy.ReviewThreshold, "TRY", transfersInWindow: 1);

        var flag = flags.ShouldHaveSingleItem();
        flag.Rule.ShouldBe(FraudPolicy.AmountAboveThresholdRule);
    }

    [Fact]
    public void Screen_TooManyTransfersInTheWindow_FlagsForReview()
    {
        var flags = FraudPolicy.Screen(100m, "TRY", FraudPolicy.MaxTransfersPerWindow + 1);

        var flag = flags.ShouldHaveSingleItem();
        flag.Rule.ShouldBe(FraudPolicy.HighVelocityRule);
    }

    [Fact]
    public void Screen_ExactlyAtTheVelocityCap_DoesNotFlag()
    {
        var flags = FraudPolicy.Screen(100m, "TRY", FraudPolicy.MaxTransfersPerWindow);

        flags.ShouldBeEmpty();
    }

    [Fact]
    public void Screen_CanMatchBothRulesAtOnce()
    {
        var flags = FraudPolicy.Screen(
            FraudPolicy.ReviewThreshold, "TRY", FraudPolicy.MaxTransfersPerWindow + 1);

        flags.Count.ShouldBe(2);
        flags.ShouldContain(f => f.Rule == FraudPolicy.AmountAboveThresholdRule);
        flags.ShouldContain(f => f.Rule == FraudPolicy.HighVelocityRule);
    }

    [Fact]
    public void Raise_CopiesTheFlagOntoTheAlert()
    {
        var transactionId = TransactionId.New();
        var flag = new FraudFlag("some_rule", "why it matched");
        var flaggedAt = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

        var alert = FraudAlert.Raise(transactionId, flag, flaggedAt);

        alert.TransactionId.ShouldBe(transactionId);
        alert.Rule.ShouldBe("some_rule");
        alert.Detail.ShouldBe("why it matched");
        alert.FlaggedAt.ShouldBe(flaggedAt);
        alert.Id.ShouldNotBe(Guid.Empty);
    }
}
