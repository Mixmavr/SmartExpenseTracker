using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartExpenseTracker.Api.BackgroundServices;
using SmartExpenseTracker.Application.Exceptions;
using SmartExpenseTracker.Application.Interfaces;

namespace SmartExpenseTracker.Tests.UnitTests;

public class ExchangeRateRefreshServiceTests
{
    // A fake rates service that only counts the refreshes, and can be told to fail.
    private class FakeExchangeRateService : IExchangeRateService
    {
        private readonly TaskCompletionSource _firstRefresh = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _refreshes;

        public bool Fail { get; set; }

        public int Refreshes => _refreshes;

        // Completes when the first refresh happened.
        public Task FirstRefresh => _firstRefresh.Task;

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _refreshes);
            _firstRefresh.TrySetResult();

            return Fail ? throw new ExchangeRateProviderException("The API is down.") : Task.CompletedTask;
        }

        public Task<Dictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private static ExchangeRateRefreshService CreateService(FakeExchangeRateService exchangeRateService)
    {
        var services = new ServiceCollection();
        services.AddScoped<IExchangeRateService>(_ => exchangeRateService);
        var provider = services.BuildServiceProvider();

        return new ExchangeRateRefreshService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ExchangeRateRefreshService>.Instance);
    }

    // When the app starts, the rates are refreshed right away, and not only after the first wait.
    [Fact]
    public async Task StartAsync_RefreshesTheRatesRightAway()
    {
        var exchangeRateService = new FakeExchangeRateService();
        var service = CreateService(exchangeRateService);

        await service.StartAsync(CancellationToken.None);
        await exchangeRateService.FirstRefresh.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, exchangeRateService.Refreshes);
    }

    // A failed refresh is only logged: the background service keeps running and does not stop the app.
    [Fact]
    public async Task StartAsync_WhenTheRefreshFails_TheServiceKeepsRunning()
    {
        var exchangeRateService = new FakeExchangeRateService { Fail = true };
        var service = CreateService(exchangeRateService);

        await service.StartAsync(CancellationToken.None);
        await exchangeRateService.FirstRefresh.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(200);

        Assert.False(service.ExecuteTask!.IsCompleted);
        await service.StopAsync(CancellationToken.None);
    }
}
