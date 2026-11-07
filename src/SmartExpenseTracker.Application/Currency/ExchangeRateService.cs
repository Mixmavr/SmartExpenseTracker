using Microsoft.Extensions.Caching.Memory;

namespace SmartExpenseTracker.Application.Currency;

public class ExchangeRateService : IExchangeRateService
{
    private const string BaseCurrency = "EUR";
    private const string CacheKey = "exchange-rates";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    // Static, so all requests share the same lock (the service itself is created once per request).
    private static readonly SemaphoreSlim Lock = new(1, 1);

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
        var cachedRates = GetFromCache();
        if (cachedRates != null)
        {
            return cachedRates;
        }

        // 2. Only one request at a time may ask the API. The others wait here.
        await Lock.WaitAsync(cancellationToken);
        try
        {
            // 3. While we were waiting, another request may have already filled the cache.
            cachedRates = GetFromCache();
            if (cachedRates != null)
            {
                return cachedRates;
            }

            // 4. Still nothing: ask the API and keep the answer for the next hour.
            var rates = await _provider.GetLatestRatesAsync(BaseCurrency, cancellationToken);
            _cache.Set(CacheKey, rates, CacheDuration);

            return rates;
        }
        finally
        {
            Lock.Release();
        }
    }

    private Dictionary<string, decimal>? GetFromCache()
    {
        _cache.TryGetValue(CacheKey, out Dictionary<string, decimal>? rates);
        return rates;
    }
}
