using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Domain.Accounts;

/// <summary>
/// A bank account. Note there is no balance field on purpose: the balance is
/// always calculated from the ledger entries posted against the account.
/// </summary>
public sealed class Account
{
    /// <summary>Default daily transfer cap for customer accounts, in the account's own currency (per UTC day).</summary>
    public const decimal DefaultDailyTransferLimit = 20_000m;

    /// <summary>Owner name of the bank's own cash accounts.</summary>
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

    /// <summary>How much the account can send by transfer per UTC day, in its own currency.</summary>
    public decimal DailyTransferLimit { get; }

    public bool IsClosed => Status == AccountStatus.Closed;

    public bool IsKycVerified => KycStatus == KycStatus.Verified;

    /// <summary>
    /// Counts state changes on this account. Ledger entries are append-only, so
    /// two concurrent movements would never conflict on their own; bumping this
    /// counter on every movement is what makes them conflict (it's the optimistic
    /// concurrency token).
    /// </summary>
    public long Version { get; private set; }

    /// <summary>Call this once for every movement posted against the account.</summary>
    public void RecordMovement() => Version++;

    /// <summary>Opens a customer deposit account (a liability from the bank's point of view). KYC starts as Pending.</summary>
    public static Result<Account> Open(string owner, Currency currency)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            return Result.Failure<Account>(AccountErrors.OwnerRequired);
        }

        return Result.Success(new Account(
            AccountId.New(), owner.Trim(), currency, AccountType.Liability, AccountStatus.Active, KycStatus.Pending));
    }

    /// <summary>Opens the bank's own cash account (asset side) for a currency. KYC doesn't apply here.</summary>
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

    /// <summary>An account can only be closed once its ledger balance is zero.</summary>
    public const string BalanceMustBeZero = "account.balance_must_be_zero";
}
