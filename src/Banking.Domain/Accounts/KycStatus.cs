namespace Banking.Domain.Accounts;

/// <summary>
/// Know-your-customer verification state. Accounts start unverified and may
/// not send transfers until verification completes; receiving stays allowed.
/// </summary>
public enum KycStatus
{
    Pending,
    Verified,
}
