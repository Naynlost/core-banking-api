using Banking.Application.Messaging;

namespace Banking.Application.Fraud.ResolveFraudAlert;

/// <summary>
/// Closes an open alert: <paramref name="Resolution"/> is "Confirmed" (real
/// fraud) or "Dismissed" (false positive), with an optional reviewer note.
/// </summary>
public sealed record ResolveFraudAlertCommand(
    Guid AlertId,
    string Resolution,
    string? Note) : ICommand;
