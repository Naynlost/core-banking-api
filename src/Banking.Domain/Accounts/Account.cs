using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Accounts;

// Bilerek balance alanı yok, bakiye her zaman ledger kayıtlarından hesaplanır
public sealed class Account
{
    public const decimal DefaultDailyTransferLimit = 20_000m;

    public const string SystemOwner = "SYSTEM";

    private Account(
        AccountId id, string owner, Currency currency, AccountType type, AccountStatus status, KycStatus kycStatus)
    {
        Id = id;
        Owner = owner;
        Currency = currency;
        Type = type;
        Status = status;
        KycStatus = kycStatus;
        DailyTransferLimit = DefaultDailyTransferLimit;
    }

    public AccountId Id { get; }

    public string Owner { get; }

    public Currency Currency { get; }

    public AccountType Type { get; }

    public AccountStatus Status { get; private set; }

    public KycStatus KycStatus { get; private set; }

    public decimal DailyTransferLimit { get; }

    public bool IsClosed => Status == AccountStatus.Closed;

    public bool IsKycVerified => KycStatus == KycStatus.Verified;

    // Ledger append-only olduğundan eş zamanlı hareketler kendiliğinden çakışmaz,
    // bu sayaç her harekette artarak optimistic locking token'ı görevi görür
    public long Version { get; private set; }

    public void RecordMovement() => Version++;

    public static Result<Account> Open(string owner, Currency currency)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            return Result.Failure<Account>(AccountErrors.OwnerRequired);
        }

        return Result.Success(new Account(
            AccountId.New(), owner.Trim(), currency, AccountType.Liability, AccountStatus.Active, KycStatus.Pending));
    }

    public static Account OpenCash(Currency currency) =>
        new(AccountId.New(), SystemOwner, currency, AccountType.Asset, AccountStatus.Active, KycStatus.Verified);

    public Result CompleteKyc()
    {
        if (IsClosed)
        {
            return Result.Failure(AccountErrors.Closed);
        }

        if (IsKycVerified)
        {
            return Result.Failure(AccountErrors.KycAlreadyVerified);
        }

        KycStatus = KycStatus.Verified;
        Version++;
        return Result.Success();
    }

    public Result Close()
    {
        if (IsClosed)
        {
            return Result.Failure(AccountErrors.AlreadyClosed);
        }

        Status = AccountStatus.Closed;
        Version++;
        return Result.Success();
    }
}

public static class AccountErrors
{
    public const string OwnerRequired = "account.owner_required";
    public const string AlreadyClosed = "account.already_closed";
    public const string Closed = "account.closed";
    public const string KycNotVerified = "account.kyc_not_verified";
    public const string KycAlreadyVerified = "account.kyc_already_verified";

    public const string BalanceMustBeZero = "account.balance_must_be_zero";
}
