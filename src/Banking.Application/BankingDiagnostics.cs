using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Banking.Application;

/// <summary>
/// Single home for the application's telemetry instruments (BCL only — no
/// vendor dependency). The names are public contract: the OpenTelemetry
/// pipeline in the host subscribes to them, and Grafana queries reference the
/// metric names derived from them.
/// </summary>
public static class BankingDiagnostics
{
    public const string ActivitySourceName = "Banking.Application";

    public const string MeterName = "Banking";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly Meter Meter = new(MeterName);

    /// <summary>Transfer commands by outcome ("success" or the error code).</summary>
    public static readonly Counter<long> Transfers = Meter.CreateCounter<long>(
        "banking.transfers", description: "Transfer commands handled, tagged by outcome.");

    /// <summary>Fraud alerts raised by the screening consumer, tagged by rule.</summary>
    public static readonly Counter<long> FraudAlerts = Meter.CreateCounter<long>(
        "banking.fraud_alerts", description: "Fraud alerts raised, tagged by rule.");
}
