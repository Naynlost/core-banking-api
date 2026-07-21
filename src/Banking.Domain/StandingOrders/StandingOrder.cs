using Banking.Domain.Accounts;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.StandingOrders;

public enum StandingOrderFrequency
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
}

public enum StandingOrderStatus
{
    Active = 0,
    Cancelled = 1,
}

public static class StandingOrderErrors
{
    public const string SameAccount = "standing_order.same_account";
    public const string AmountMustBePositive = "standing_order.amount_must_be_positive";
    public const string AlreadyCancelled = "standing_order.already_cancelled";
}

// Sadece zamanlar; her tekrar normal transfer olarak çalıştığından KYC/bakiye/limit kuralları çalışma anında uygulanır
public sealed class StandingOrder
{
    // EF'in nesne oluşturması için, veri yazılırken zaten doğrulanmıştı
    private StandingOrder()
    {
        Owner = null!;
        Amount = null!;
    }

    private StandingOrder(
        Guid id,
        string owner,
        AccountId sourceAccountId,
        AccountId destinationAccountId,
        Money amount,
        StandingOrderFrequency frequency,
        DateTimeOffset firstRunAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        Owner = owner;
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
        Amount = amount;
        Frequency = frequency;
        NextRunAt = firstRunAt;
        CreatedAt = createdAt;
        Status = StandingOrderStatus.Active;
    }

    public Guid Id { get; }

    public string Owner { get; }

    public AccountId SourceAccountId { get; }

    public AccountId DestinationAccountId { get; }

    public Money Amount { get; }

    public StandingOrderFrequency Frequency { get; }

    public StandingOrderStatus Status { get; private set; }

    public DateTimeOffset NextRunAt { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? LastRunAt { get; private set; }

    public string? LastRunError { get; private set; }

    public bool IsActive => Status == StandingOrderStatus.Active;

    // (order, planlanan zaman) çiftinden türer; çökme sonrası tekrar çalıştırma aynı occurrence'ı iki kez işlemez
    public string CurrentRunKey => $"so-{Id:N}-{NextRunAt.UtcTicks}";

    public static Result<StandingOrder> Create(
        string owner,
        AccountId sourceAccountId,
        AccountId destinationAccountId,
        Money amount,
        StandingOrderFrequency frequency,
        DateTimeOffset firstRunAt,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            return Result.Failure<StandingOrder>(AccountErrors.OwnerRequired);
        }

        if (sourceAccountId == destinationAccountId)
        {
            return Result.Failure<StandingOrder>(StandingOrderErrors.SameAccount);
        }

        if (amount.IsZero)
        {
            return Result.Failure<StandingOrder>(StandingOrderErrors.AmountMustBePositive);
        }

        return Result.Success(new StandingOrder(
            Guid.NewGuid(), owner.Trim(), sourceAccountId, destinationAccountId,
            amount, frequency, firstRunAt, createdAt));
    }

    public Result Cancel()
    {
        if (!IsActive)
        {
            return Result.Failure(StandingOrderErrors.AlreadyCancelled);
        }

        Status = StandingOrderStatus.Cancelled;
        return Result.Success();
    }

    // Sonraki zaman planlanan zamandan ilerler, çalıştığı andan değil; böylece plan kaymaz
    public void RecordRun(DateTimeOffset ranAt, string? error)
    {
        LastRunAt = ranAt;
        LastRunError = error;
        NextRunAt = Frequency switch
        {
            StandingOrderFrequency.Daily => NextRunAt.AddDays(1),
            StandingOrderFrequency.Weekly => NextRunAt.AddDays(7),
            _ => NextRunAt.AddMonths(1),
        };
    }
}
