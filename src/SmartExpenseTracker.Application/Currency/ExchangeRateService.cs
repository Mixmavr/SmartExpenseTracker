using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace SmartExpenseTracker.Application.Currency;

public class ExchangeRateService : IExchangeRateService
{
    public const string CacheKey = "exchange-rates";
    private const string LastKnownKey = "exchange-rates-last-known";
    private const string BaseCurrency = "EUR";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan FallbackCacheDuration = TimeSpan.FromMinutes(1);

    // Static, so all requests share the same lock (the service itself is created once per request).
    private static readonly SemaphoreSlim Lock = new(1, 1);

    private readonly IExchangeRateProvider _provider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ExchangeRateService> _logger;

    public ExchangeRateService(IExchangeRateProvider provider, IMemoryCache cache, ILogger<ExchangeRateService> logger)
    {
        _provider = provider;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Dictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken)
    {
        // 1. If we already have the rates in memory, use them.
        var cachedRates = GetFromCache(CacheKey);
        if (cachedRates != null)
        {
            return cachedRates;
        }

        // 2. Only one request at a time may ask the API. The others wait here.
        await Lock.WaitAsync(cancellationToken);
        try
        {
            // 3. While we were waiting, another request may have already filled the cache.
            cachedRates = GetFromCache(CacheKey);
            if (cachedRates != null)
            {
                return cachedRates;
            }

            // 4. Still nothing: ask the API.
            try
            {
                var rates = await _provider.GetLatestRatesAsync(BaseCurrency, cancellationToken);
                _cache.Set(CacheKey, rates, CacheDuration);
                _cache.Set(LastKnownKey, rates); // no expiration: our backup copy
                return rates;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "The exchange rate API failed. Trying the last known rates.");

                // 5. The API failed: use the backup copy if we have one.
                var lastKnownRates = GetFromCache(LastKnownKey);
                if (lastKnownRates == null)
                {
                    throw;
                }

                // Keep them for a minute so we do not call the failing API again for every request.
                _cache.Set(CacheKey, lastKnownRates, FallbackCacheDuration);
                return lastKnownRates;
            }
        }
        finally
        {
            Lock.Release();
        }
    }

    private Dictionary<string, decimal>? GetFromCache(string key)
    {
        _cache.TryGetValue(key, out Dictionary<string, decimal>? rates);
        return rates;
    }
}
