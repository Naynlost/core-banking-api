using Banking.Domain.Ledgers;
using Banking.Domain.Primitives;

namespace Banking.Domain.Fraud;

public static class FraudAlertErrors
{
    public const string AlreadyResolved = "fraud_alert.already_resolved";
    public const string InvalidResolution = "fraud_alert.invalid_resolution";
}

// Şüpheli işlemi işaretler, ledger'a dokunmaz; inceleme için bir kayıttır
public sealed class FraudAlert
{
    // EF'in nesne oluşturması için, veri yazılırken zaten doğrulanmıştı
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

    public FraudAlertStatus Status { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public string? ResolutionNote { get; private set; }

    public static FraudAlert Raise(TransactionId transactionId, FraudFlag flag, DateTimeOffset flaggedAt) =>
        new(Guid.NewGuid(), transactionId, flag.Rule, flag.Detail, flaggedAt);

    // İnceleme tek seferde kapanır, karar sonradan değiştirilemez
    public Result Resolve(FraudAlertStatus resolution, string? note, DateTimeOffset resolvedAt)
    {
        if (resolution == FraudAlertStatus.Open)
        {
            return Result.Failure(FraudAlertErrors.InvalidResolution);
        }

        if (Status != FraudAlertStatus.Open)
        {
            return Result.Failure(FraudAlertErrors.AlreadyResolved);
        }

        Status = resolution;
        ResolvedAt = resolvedAt;
        ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return Result.Success();
    }
}
