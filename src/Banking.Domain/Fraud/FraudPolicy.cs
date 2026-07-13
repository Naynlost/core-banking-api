namespace Banking.Domain.Fraud;

/// <summary>A fraud rule that matched a transfer, with a human-readable reason.</summary>
public sealed record FraudFlag(string Rule, string Detail);

/// <summary>
/// Rule-based screening of committed transfers. Screening never blocks a
/// transfer — it already happened; a match only marks the transaction for
/// review. The rules are pure: callers supply the ledger-derived inputs.
/// </summary>
public static class FraudPolicy
{
    public const string AmountAboveThresholdRule = "amount_above_threshold";
    public const string HighVelocityRule = "high_velocity";

    /// <summary>Transfers at or above this amount are flagged for review.</summary>
    public const decimal ReviewThreshold = 10_000m;

    /// <summary>Window inspected by the velocity rule, ending at the transfer being screened.</summary>
    public static readonly TimeSpan VelocityWindow = TimeSpan.FromMinutes(10);

    /// <summary>Most transfers (the screened one included) a source account may make within the window.</summary>
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
