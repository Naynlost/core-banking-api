namespace Banking.Api.Contracts;

public sealed record CreateAccountRequest(string CurrencyCode);

public sealed record CreateAccountResponse(Guid Id);
