namespace Banking.Domain.Fraud;

/// <summary>
/// Review lifecycle of an alert: it opens as a work item and a reviewer closes
/// it exactly once, either confirming the fraud or dismissing a false positive.
/// </summary>
public enum FraudAlertStatus
{
    Open = 0,
    Confirmed = 1,
    Dismissed = 2,
}
