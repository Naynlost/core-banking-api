using Banking.Application.Messaging;

namespace Banking.Application.Accounts.CreateAccount;

/// <summary>Opens a deposit account for the given owner; returns the new account id.</summary>
public sealed record CreateAccountCommand(string Owner, string CurrencyCode) : ICommand<Guid>;
