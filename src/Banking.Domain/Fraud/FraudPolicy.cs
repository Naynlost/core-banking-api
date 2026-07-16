namespace Banking.Domain.Fraud;

/// <summary>A fraud rule that matched a transfer, plus a readable reason.</summary>
public sealed record FraudFlag(string Rule, string Detail);

/// <summary>
/// Rule-based screening of committed transfers. By the time we screen, the
/// transfer has already happened, so a match doesn't block anything; it just
/// marks the transaction for review. The rules are pure functions and the
/// caller supplies whatever has to come from the ledger.
/// </summary>
public static class FraudPolicy
{
    public const string AmountAboveThresholdRule = "amount_above_threshold";
    public const string HighVelocityRule = "high_velocity";

    /// <summary>Transfers at or above this amount get flagged for review.</summary>
    public const decimal ReviewThreshold = 10_000m;

    /// <summary>How far back the velocity rule looks, counting up to the screened transfer.</summary>
    public static readonly TimeSpan VelocityWindow = TimeSpan.FromMinutes(10);

    /// <summary>Maximum transfers (including the screened one) an account may send within the window.</summary>
    public const int MaxTransfersPerWindow = 5;

    public static IReadOnlyList<FraudFlag> Screen(decimal amount, string currencyCode, int transfersInWindow)
    {
        var flags = new List<FraudFlag>();

        if (amount >= ReviewThreshold)
        {
            flags.Add(new FraudFlag(
                AmountAboveThresholdRule,
                $"{amount} {currencyCode} meets the review threshold of {ReviewThreshold}."));
        }

        if (transfersInWindow > MaxTransfersPerWindow)
        {
            flags.Add(new FraudFlag(
                HighVelocityRule,
                $"{transfersInWindow} transfers within {VelocityWindow.TotalMinutes:0} minutes "
                + $"exceeds the allowed {MaxTransfersPerWindow}."));
        }

        return flags;
    }
}
