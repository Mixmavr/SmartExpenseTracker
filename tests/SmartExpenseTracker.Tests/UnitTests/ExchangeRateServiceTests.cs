using Microsoft.Extensions.Caching.Memory;
using SmartExpenseTracker.Application.Currency;

namespace SmartExpenseTracker.Tests.UnitTests;

public class ExchangeRateServiceTests
{
    // A fake provider that counts how many times it was called.
    private class FakeExchangeRateProvider : IExchangeRateProvider
    {
        public int Calls { get; private set; }

        public Task<Dictionary<string, decimal>> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new Dictionary<string, decimal> { ["USD"] = 1.25m });
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
}
