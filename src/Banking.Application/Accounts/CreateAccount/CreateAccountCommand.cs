using Banking.Application.Messaging;

namespace Banking.Application.Accounts.CreateAccount;

public sealed record CreateAccountCommand(string Owner, string CurrencyCode) : ICommand<Guid>;
