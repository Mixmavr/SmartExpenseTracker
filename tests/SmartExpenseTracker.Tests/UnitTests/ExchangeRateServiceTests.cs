using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SmartExpenseTracker.Application.Currency;

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

        public async Task<Dictionary<string, decimal>> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            await Task.Delay(Delay, cancellationToken);

            if (Fail)
            {
                throw new ExchangeRateProviderException("The API is down.");
            }

            return new Dictionary<string, decimal> { ["USD"] = 1.25m };
        }
    }

    private static ExchangeRateService CreateService(FakeExchangeRateProvider provider, IMemoryCache cache)
    {
        return new ExchangeRateService(provider, cache, NullLogger<ExchangeRateService>.Instance);
    }

    [Fact]
    public async Task GetRatesAsync_ReturnsTheRatesFromTheProvider()
    {
        var provider = new FakeExchangeRateProvider();
        var service = CreateService(provider, new MemoryCache(new MemoryCacheOptions()));

        var rates = await service.GetRatesAsync(CancellationToken.None);

        Assert.Equal(1.25m, rates["USD"]);
    }

    [Fact]
    public async Task GetRatesAsync_CalledTwice_AsksTheProviderOnlyOnce()
    {
        var provider = new FakeExchangeRateProvider();
        var service = CreateService(provider, new MemoryCache(new MemoryCacheOptions()));

        await service.GetRatesAsync(CancellationToken.None);
        await service.GetRatesAsync(CancellationToken.None);

        Assert.Equal(1, provider.Calls);
    }

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

    [Fact]
    public async Task GetRatesAsync_ApiFailsAndThereAreNoLastKnownRates_ThrowsTheError()
    {
        var provider = new FakeExchangeRateProvider { Fail = true };
        var service = CreateService(provider, new MemoryCache(new MemoryCacheOptions()));

        await Assert.ThrowsAsync<ExchangeRateProviderException>(() => service.GetRatesAsync(CancellationToken.None));
    }

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
}
