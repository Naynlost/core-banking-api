using Banking.Application.Messaging;

namespace Banking.Application.Accounts.CompleteKyc;

// Gerçek KYC akışının (belge kontrolü, onay) yerine geçen demo endpoint'i
public sealed record CompleteKycCommand(Guid AccountId, string Requester) : ICommand;
