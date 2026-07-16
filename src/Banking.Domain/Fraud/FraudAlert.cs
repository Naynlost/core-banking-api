using Banking.Domain.Ledgers;

namespace Banking.Domain.Fraud;

/// <summary>
/// Records that a committed transaction looked suspicious: which rule matched
/// and why. The ledger itself is left alone; an alert is a work item for
/// review, not a financial record.
/// </summary>
public sealed class FraudAlert
{
    // For EF materialization only; the data was validated when it was written.
    private FraudAlert()
    {
        Rule = null!;
        Detail = null!;
    }

    private FraudAlert(Guid id, TransactionId transactionId, string rule, string detail, DateTimeOffset flaggedAt)
    {
        Id = id;
        TransactionId = transactionId;
        Rule = rule;
        Detail = detail;
        FlaggedAt = flaggedAt;
    }

    public Guid Id { get; }

    public TransactionId TransactionId { get; }

    public string Rule { get; }

    public string Detail { get; }

    public DateTimeOffset FlaggedAt { get; }

    public static FraudAlert Raise(TransactionId transactionId, FraudFlag flag, DateTimeOffset flaggedAt) =>
        new(Guid.NewGuid(), transactionId, flag.Rule, flag.Detail, flaggedAt);
}
