using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SmartExpenseTracker.Application.Exceptions;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Models;
using SmartExpenseTracker.Application.Services;

namespace SmartExpenseTracker.Tests.UnitTests;

public class ExchangeRateServiceTests
{
    // A fake provider that counts how many times it was called.
    private class FakeExchangeRateProvider : IExchangeRateProvider
    {
        private int _calls;

        public int Calls => _calls;

        // Pretends the network is slow, like the real API.
        public TimeSpan Delay { get; set; }

        // Pretends the API is down.
        public bool Fail { get; set; }

        // The USD rate the API gives.
        public decimal Rate { get; set; } = 1.25m;

        public async Task<Dictionary<string, decimal>> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            await Task.Delay(Delay, cancellationToken);

            if (Fail)
            {
                throw new ExchangeRateProviderException("The API is down.");
            }

            return new Dictionary<string, decimal> { ["USD"] = Rate };
        }
    }

    private static ExchangeRateService CreateService(FakeExchangeRateProvider provider, IMemoryCache cache, StatisticsService? statistics = null)
    {
        return new ExchangeRateService(provider, cache, NullLogger<ExchangeRateService>.Instance, statistics ?? new StatisticsService());
    }

    // Cache: the second call uses the saved rates and does not ask the API again.
    [Fact]
    public async Task GetRatesAsync_CalledTwice_AsksTheProviderOnlyOnce()
    {
        var provider = new FakeExchangeRateProvider();
        var service = CreateService(provider, new MemoryCache(new MemoryCacheOptions()));

        await service.GetRatesAsync(CancellationToken.None);
        await service.GetRatesAsync(CancellationToken.None);

        Assert.Equal(1, provider.Calls);
    }

    // Concurrency: 20 requests at the same time still cause only one API call.
    [Fact]
    public async Task GetRatesAsync_ManyRequestsAtTheSameTime_AsksTheProviderOnlyOnce()
    {
        var provider = new FakeExchangeRateProvider { Delay = TimeSpan.FromMilliseconds(200) };
        var cache = new MemoryCache(new MemoryCacheOptions());

        // 20 requests arrive together, each one gets its own service (like in the real app).
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => CreateService(provider, cache).GetRatesAsync(CancellationToken.None)))
            .ToList();
        await Task.WhenAll(tasks);

        Assert.Equal(1, provider.Calls);
    }

    // Fallback: when the cache expired and the API is down, we use the backup rates.
    [Fact]
    public async Task GetRatesAsync_ApiFailsAfterTheCacheExpired_ReturnsTheLastKnownRates()
    {
        var provider = new FakeExchangeRateProvider();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(provider, cache);
        await service.GetRatesAsync(CancellationToken.None);

        // The cache entry expires, and then the API goes down.
        cache.Remove(ExchangeRateService.CacheKey);
        provider.Fail = true;
        var rates = await service.GetRatesAsync(CancellationToken.None);

        Assert.Equal(1.25m, rates["USD"]);
    }

    // Error: when the API is down and there is no backup, the error is not hidden.
    [Fact]
    public async Task GetRatesAsync_ApiFailsAndThereAreNoLastKnownRates_ThrowsTheError()
    {
        var provider = new FakeExchangeRateProvider { Fail = true };
        var service = CreateService(provider, new MemoryCache(new MemoryCacheOptions()));

        await Assert.ThrowsAsync<ExchangeRateProviderException>(() => service.GetRatesAsync(CancellationToken.None));
    }

    // Fallback cache: after using the backup, the next requests do not call the failing API again.
    [Fact]
    public async Task GetRatesAsync_UsingTheLastKnownRates_DoesNotCallTheFailingApiAgainRightAway()
    {
        var provider = new FakeExchangeRateProvider();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(provider, cache);
        await service.GetRatesAsync(CancellationToken.None);
        cache.Remove(ExchangeRateService.CacheKey);
        provider.Fail = true;

        await service.GetRatesAsync(CancellationToken.None);
        await service.GetRatesAsync(CancellationToken.None);
        await service.GetRatesAsync(CancellationToken.None);

        // 1 successful call at the start + 1 failed call. The next two requests used the saved rates.
        Assert.Equal(2, provider.Calls);
    }

    // Statistics: the first request calls the API, the second one is answered by the cache.
    [Fact]
    public async Task GetRatesAsync_CountsTheCacheHitsAndTheApiCalls()
    {
        var statistics = new StatisticsService();
        var service = CreateService(new FakeExchangeRateProvider(), new MemoryCache(new MemoryCacheOptions()), statistics);

        await service.GetRatesAsync(CancellationToken.None);
        await service.GetRatesAsync(CancellationToken.None);

        Assert.Equal(new ExchangeRateStatisticsDto(1, 1, 0, 0), statistics.GetSnapshot().ExchangeRates);
    }

    // Statistics: 20 requests at the same time give 1 API call and 19 cache hits (the ones that waited).
    [Fact]
    public async Task GetRatesAsync_ManyRequestsAtTheSameTime_CountsOneApiCallAndTheRestAsCacheHits()
    {
        var statistics = new StatisticsService();
        var provider = new FakeExchangeRateProvider { Delay = TimeSpan.FromMilliseconds(200) };
        var cache = new MemoryCache(new MemoryCacheOptions());

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => CreateService(provider, cache, statistics).GetRatesAsync(CancellationToken.None)))
            .ToList();
        await Task.WhenAll(tasks);

        Assert.Equal(new ExchangeRateStatisticsDto(19, 1, 0, 0), statistics.GetSnapshot().ExchangeRates);
    }

    // Statistics: a failed API call and the use of the backup rates are counted.
    [Fact]
    public async Task GetRatesAsync_ApiFails_CountsTheFailureAndTheFallback()
    {
        var statistics = new StatisticsService();
        var provider = new FakeExchangeRateProvider();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(provider, cache, statistics);
        await service.GetRatesAsync(CancellationToken.None);
        cache.Remove(ExchangeRateService.CacheKey);
        provider.Fail = true;

        await service.GetRatesAsync(CancellationToken.None);

        // 2 API calls (1 worked, 1 failed), 1 failure, 1 use of the backup, and no cache hit.
        Assert.Equal(new ExchangeRateStatisticsDto(0, 2, 1, 1), statistics.GetSnapshot().ExchangeRates);
    }

    // Background refresh: it fills the cache, so the next request does not call the API.
    [Fact]
    public async Task RefreshAsync_FillsTheCache_SoTheNextRequestDoesNotCallTheApi()
    {
        var provider = new FakeExchangeRateProvider();
        var service = CreateService(provider, new MemoryCache(new MemoryCacheOptions()));

        await service.RefreshAsync(CancellationToken.None);
        await service.GetRatesAsync(CancellationToken.None);

        Assert.Equal(1, provider.Calls);
    }

    // Background refresh: it always asks the API and replaces the saved rates with the new ones.
    [Fact]
    public async Task RefreshAsync_ReplacesTheSavedRates()
    {
        var provider = new FakeExchangeRateProvider();
        var service = CreateService(provider, new MemoryCache(new MemoryCacheOptions()));
        await service.GetRatesAsync(CancellationToken.None);

        provider.Rate = 1.30m;
        await service.RefreshAsync(CancellationToken.None);
        var rates = await service.GetRatesAsync(CancellationToken.None);

        Assert.Equal(1.30m, rates["USD"]);
        Assert.Equal(2, provider.Calls);
    }

    // Background refresh: when the API is down, the error is thrown (the background service logs it) and the saved rates stay.
    [Fact]
    public async Task RefreshAsync_ApiFails_ThrowsAndKeepsTheSavedRates()
    {
        var provider = new FakeExchangeRateProvider();
        var service = CreateService(provider, new MemoryCache(new MemoryCacheOptions()));
        await service.GetRatesAsync(CancellationToken.None);
        provider.Fail = true;

        await Assert.ThrowsAsync<ExchangeRateProviderException>(() => service.RefreshAsync(CancellationToken.None));
        var rates = await service.GetRatesAsync(CancellationToken.None);

        Assert.Equal(1.25m, rates["USD"]);
        Assert.Equal(2, provider.Calls);
    }

    // Background refresh: it also saves the backup copy, used later if the cache expires and the API is down.
    [Fact]
    public async Task RefreshAsync_SavesTheBackupCopy()
    {
        var provider = new FakeExchangeRateProvider();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(provider, cache);
        await service.RefreshAsync(CancellationToken.None);

        cache.Remove(ExchangeRateService.CacheKey);
        provider.Fail = true;
        var rates = await service.GetRatesAsync(CancellationToken.None);

        Assert.Equal(1.25m, rates["USD"]);
    }

    // Statistics: a refresh counts as an API call, and a failed refresh also counts as a failure.
    [Fact]
    public async Task RefreshAsync_CountsTheApiCallsAndTheFailure()
    {
        var statistics = new StatisticsService();
        var provider = new FakeExchangeRateProvider();
        var service = CreateService(provider, new MemoryCache(new MemoryCacheOptions()), statistics);

        await service.RefreshAsync(CancellationToken.None);
        provider.Fail = true;
        await Assert.ThrowsAsync<ExchangeRateProviderException>(() => service.RefreshAsync(CancellationToken.None));

        Assert.Equal(new ExchangeRateStatisticsDto(0, 2, 1, 0), statistics.GetSnapshot().ExchangeRates);
    }
}
