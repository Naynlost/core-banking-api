namespace Banking.Domain.Accounts;

/// <summary>
/// KYC verification state. New accounts start as Pending and can't send
/// transfers until verified. Receiving money is still allowed.
/// </summary>
public enum KycStatus
{
    Pending,
    Verified,
}
