using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Models;

namespace SmartExpenseTracker.Application.Services;

public class ExchangeRateService : IExchangeRateService
{
    public const string CacheKey = "exchange-rates";
    public const string BaseCurrency = "EUR";
    private const string BackupKey = "exchange-rates-backup";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan FallbackCacheDuration = TimeSpan.FromMinutes(1);

    // Static, so all requests share the same lock (the service itself is created once per request).
    private static readonly SemaphoreSlim Lock = new(1, 1);

    private readonly IExchangeRateProvider _provider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ExchangeRateService> _logger;
    private readonly IStatisticsService _statistics;

    public ExchangeRateService(IExchangeRateProvider provider, IMemoryCache cache, ILogger<ExchangeRateService> logger, IStatisticsService statistics)
    {
        _provider = provider;
        _cache = cache;
        _logger = logger;
        _statistics = statistics;
    }

    public async Task<Dictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken)
    {
        // 1. If we already have the rates in memory
        var cachedRates = GetFromCache(CacheKey);
        if (cachedRates != null)
        {
            _statistics.RecordExchangeRateEvent(ExchangeRateEvent.CacheHit);
            return cachedRates;
        }

        await Lock.WaitAsync(cancellationToken);
        try
        {
            // 2. While we were waiting, another request may have already filled the cache.
            cachedRates = GetFromCache(CacheKey);
            if (cachedRates != null)
            {
                _statistics.RecordExchangeRateEvent(ExchangeRateEvent.CacheHit);
                return cachedRates;
            }

            try
            {
                _statistics.RecordExchangeRateEvent(ExchangeRateEvent.ProviderCall);
                var rates = await _provider.GetLatestRatesAsync(BaseCurrency, cancellationToken);
                _cache.Set(CacheKey, rates, CacheDuration);
                _cache.Set(BackupKey, rates); // Backup copy
                return rates;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "The exchange rate API failed. Trying the last known rates.");
                _statistics.RecordExchangeRateEvent(ExchangeRateEvent.ProviderFailure);

                // 3. Use backup if API fail
                var backupRates = GetFromCache(BackupKey);
                if (backupRates == null)
                {
                    throw;
                }

                // Fill with backup for next 1 min
                _statistics.RecordExchangeRateEvent(ExchangeRateEvent.FallbackUsed);
                _cache.Set(CacheKey, backupRates, FallbackCacheDuration);
                return backupRates;
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
