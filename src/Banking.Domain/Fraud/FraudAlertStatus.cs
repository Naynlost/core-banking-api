namespace Banking.Domain.Fraud;

// Reviewer, açık uyarıyı bir kez kapatır: onaylar ya da yanlış pozitif olarak reddeder
public enum FraudAlertStatus
{
    Open = 0,
    Confirmed = 1,
    Dismissed = 2,
}
