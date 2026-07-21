using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;

namespace Banking.Application.Abstractions;

// Kur kaynağı dış dünyadadır: bugün yapılandırmadan okunuyor, yarın bir kur beslemesi
// takılabilir. Domain bu arayüzü tanımaz; çevrim matematiği ExchangeRate'in içindedir.
public interface IExchangeRateProvider
{
    Task<Result<ExchangeRate>> GetRateAsync(Currency from, Currency to, CancellationToken cancellationToken);
}
