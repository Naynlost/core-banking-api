namespace Banking.Domain.Fraud;

public sealed record FraudFlag(string Rule, string Detail);

// Tarama transferden SONRA çalışır, eşleşme işlemi durdurmaz sadece incelemeye işaretler
public static class FraudPolicy
{
    public const string AmountAboveThresholdRule = "amount_above_threshold";
    public const string HighVelocityRule = "high_velocity";

    public const decimal ReviewThreshold = 10_000m;

    public static readonly TimeSpan VelocityWindow = TimeSpan.FromMinutes(10);

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
