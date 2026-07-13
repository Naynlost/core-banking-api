using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Accounts;

/// <summary>
/// A bank account. Holds no balance field: the balance is always derived
/// from the ledger entries posted against the account.
/// </summary>
public sealed class Account
{
    /// <summary>Default cap on the total a customer account may send by transfer per UTC day, in the account's currency.</summary>
    public const decimal DefaultDailyTransferLimit = 20_000m;

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

    /// <summary>Total the account may send by transfer per UTC day, in the account's currency.</summary>
    public decimal DailyTransferLimit { get; }

    public bool IsClosed => Status == AccountStatus.Closed;

    public bool IsKycVerified => KycStatus == KycStatus.Verified;

    /// <summary>
    /// Number of state changes recorded against this account. Ledger entries are
    /// append-only, so two concurrent movements never collide on data — this counter
    /// is the optimistic concurrency token that forces such writes to conflict.
    /// </summary>
    public long Version { get; private set; }

    /// <summary>Must be called once for every movement posted against this account.</summary>
    public void RecordMovement() => Version++;

    /// <summary>Opens a customer deposit account (a liability from the bank's perspective). KYC starts pending.</summary>
    public static Result<Account> Open(string owner, Currency currency)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            return Result.Failure<Account>(AccountErrors.OwnerRequired);
        }

        return Result.Success(new Account(
            AccountId.New(), owner.Trim(), currency, AccountType.Liability, AccountStatus.Active, KycStatus.Pending));
    }

    /// <summary>Opens the bank's internal cash account (asset side) for a currency. Never needs KYC.</summary>
    public static Account OpenCash(Currency currency) =>
        new(AccountId.New(), "SYSTEM", currency, AccountType.Asset, AccountStatus.Active, KycStatus.Verified);

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
}
