using Microsoft.Extensions.Caching.Memory;

namespace SmartExpenseTracker.Application.Currency;

public class ExchangeRateService : IExchangeRateService
{
    private const string BaseCurrency = "EUR";
    private const string CacheKey = "exchange-rates";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private readonly IExchangeRateProvider _provider;
    private readonly IMemoryCache _cache;

    public ExchangeRateService(IExchangeRateProvider provider, IMemoryCache cache)
    {
        _provider = provider;
        _cache = cache;
    }

    public async Task<Dictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken)
    {
        // 1. If we already have the rates in memory, use them.
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, decimal>? cachedRates) && cachedRates != null)
        {
            return cachedRates;
        }

        // 2. Otherwise ask the API and keep the answer for the next hour.
        var rates = await _provider.GetLatestRatesAsync(BaseCurrency, cancellationToken);
        _cache.Set(CacheKey, rates, CacheDuration);

        return rates;
    }
}
