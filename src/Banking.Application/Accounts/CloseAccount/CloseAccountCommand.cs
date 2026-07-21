using Banking.Application.Messaging;

namespace Banking.Application.Accounts.CloseAccount;

// Bakiye sıfır değilse kapatma reddedilir, aksi halde para kapalı hesapta kilitli kalır
public sealed record CloseAccountCommand(Guid AccountId, string Requester) : ICommand;
