using Microsoft.Extensions.Caching.Memory;
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

        public async Task<Dictionary<string, decimal>> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            await Task.Delay(Delay, cancellationToken);
            return new Dictionary<string, decimal> { ["USD"] = 1.25m };
        }
    }

    [Fact]
    public async Task GetRatesAsync_ReturnsTheRatesFromTheProvider()
    {
        var provider = new FakeExchangeRateProvider();
        var service = new ExchangeRateService(provider, new MemoryCache(new MemoryCacheOptions()));

        var rates = await service.GetRatesAsync(CancellationToken.None);

        Assert.Equal(1.25m, rates["USD"]);
    }

    [Fact]
    public async Task GetRatesAsync_CalledTwice_AsksTheProviderOnlyOnce()
    {
        var provider = new FakeExchangeRateProvider();
        var service = new ExchangeRateService(provider, new MemoryCache(new MemoryCacheOptions()));

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
            .Select(_ => Task.Run(() => new ExchangeRateService(provider, cache).GetRatesAsync(CancellationToken.None)))
            .ToList();
        await Task.WhenAll(tasks);

        Assert.Equal(1, provider.Calls);
    }
}
