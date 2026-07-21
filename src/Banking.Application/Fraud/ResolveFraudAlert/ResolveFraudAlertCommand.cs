using Banking.Application.Messaging;

namespace Banking.Application.Fraud.ResolveFraudAlert;

// Resolution "Confirmed" (gerçek fraud) veya "Dismissed" (yanlış pozitif) olabilir
public sealed record ResolveFraudAlertCommand(
    Guid AlertId,
    string Resolution,
    string? Note) : ICommand;
