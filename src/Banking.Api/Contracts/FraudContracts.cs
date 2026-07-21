namespace Banking.Api.Contracts;

// Resolution "Confirmed" (gerçek fraud) veya "Dismissed" (yanlış pozitif) olabilir
public sealed record ResolveFraudAlertRequest(string Resolution, string? Note);
