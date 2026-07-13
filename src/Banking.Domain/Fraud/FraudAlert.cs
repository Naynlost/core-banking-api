using Banking.Domain.Ledgers;

namespace Banking.Domain.Fraud;

/// <summary>
/// Marks a committed transaction as suspicious: which rule matched and why.
/// The ledger itself stays untouched — an alert is a review work item, not a
/// financial record, so flagging never mutates immutable entries.
/// </summary>
public sealed class FraudAlert
{
    // Materialization-only constructor: filled by the persistence layer from
    // already-validated rows.
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
