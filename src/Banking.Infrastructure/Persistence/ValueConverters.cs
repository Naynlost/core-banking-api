using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Banking.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Banking.Infrastructure.Persistence;

// Okuma veritabanına güvenir: değerler yazılırken domain'de zaten doğrulanmıştı
internal static class ValueConverters
{
    public static readonly ValueConverter<AccountId, Guid> AccountId =
        new(id => id.Value, value => new AccountId(value));

    public static readonly ValueConverter<TransactionId, Guid> TransactionId =
        new(id => id.Value, value => new TransactionId(value));

    public static readonly ValueConverter<Currency, string> Currency =
        new(currency => currency.Code, code => Banking.Domain.ValueObjects.Currency.Create(code).Value);
}
