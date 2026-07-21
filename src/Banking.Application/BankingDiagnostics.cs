using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Banking.Application;

// İsimler public sözleşme: OpenTelemetry pipeline'ı ve Grafana sorguları bunlara bağlı
public static class BankingDiagnostics
{
    public const string ActivitySourceName = "Banking.Application";

    public const string MeterName = "Banking";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> Transfers = Meter.CreateCounter<long>(
        "banking.transfers", description: "Transfer commands handled, tagged by outcome.");

    public static readonly Counter<long> FraudAlerts = Meter.CreateCounter<long>(
        "banking.fraud_alerts", description: "Fraud alerts raised, tagged by rule.");

    public static readonly Counter<long> CashOperations = Meter.CreateCounter<long>(
        "banking.cash_operations", description: "Cash operations handled, tagged by kind and outcome.");
}
