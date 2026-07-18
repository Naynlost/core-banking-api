namespace Banking.Api.Contracts;

/// <summary>Resolution is "Confirmed" (real fraud) or "Dismissed" (false positive).</summary>
public sealed record ResolveFraudAlertRequest(string Resolution, string? Note);
