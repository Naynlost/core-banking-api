using Banking.Application.Abstractions;
using Banking.Domain.Primitives;
using Banking.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Banking.Infrastructure.Fx;

// Kurlar yapılandırmadan okunur. Her para birimi için "1 birim kaç baz para birimi eder"
// yazılır; çapraz kur bu ikisinin oranından çıkar. Böylece testler ve CI ağa bağlı olmaz.
// Gerçek bir kurulumda bu sınıfın yerine kur beslemesinden okuyan bir implementasyon geçer.
public sealed class FxOptions
{
    public const string SectionName = "Fx";

    // Diğer tüm kurların ifade edildiği para birimi
    public string BaseCurrency { get; init; } = "TRY";

    // Örn. "USD": 41.50 → 1 USD = 41.50 TRY. Baz para birimi yazılmasa da 1 kabul edilir.
    public Dictionary<string, decimal> Rates { get; init; } = [];
}

internal sealed class ConfiguredExchangeRateProvider(IOptions<FxOptions> options) : IExchangeRateProvider
{
    public Task<Result<ExchangeRate>> GetRateAsync(
        Currency from, Currency to, CancellationToken cancellationToken)
    {
        if (from == to)
        {
            return Task.FromResult(ExchangeRate.Create(from, to, 1m));
        }

        var fromBase = ToBaseRate(from);
        var toBase = ToBaseRate(to);

        if (fromBase is null || toBase is null)
        {
            return Task.FromResult(Result.Failure<ExchangeRate>(ExchangeRateErrors.RateNotAvailable));
        }

        // 1 from = fromBase baz; 1 to = toBase baz → 1 from = (fromBase / toBase) to
        return Task.FromResult(ExchangeRate.Create(from, to, fromBase.Value / toBase.Value));
    }

    private decimal? ToBaseRate(Currency currency)
    {
        if (string.Equals(currency.Code, options.Value.BaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        foreach (var (code, rate) in options.Value.Rates)
        {
            if (string.Equals(code, currency.Code, StringComparison.OrdinalIgnoreCase))
            {
                return rate > 0 ? rate : null;
            }
        }

        return null;
    }
}
