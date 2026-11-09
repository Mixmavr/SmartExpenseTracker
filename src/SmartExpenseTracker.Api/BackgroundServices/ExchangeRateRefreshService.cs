using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Services;

namespace SmartExpenseTracker.Api.BackgroundServices;

// Keeps the exchange rates fresh: it refreshes them when the app starts and then every hour.
public class ExchangeRateRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExchangeRateRefreshService> _logger;

    public ExchangeRateRefreshService(IServiceScopeFactory scopeFactory, ILogger<ExchangeRateRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RefreshAsync(stoppingToken);
                await Task.Delay(ExchangeRateService.RefreshInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The app is stopping.
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            // This service lives as long as the app, but the exchange rate service is created per scope.
            using var scope = _scopeFactory.CreateScope();
            var exchangeRateService = scope.ServiceProvider.GetRequiredService<IExchangeRateService>();

            await exchangeRateService.RefreshAsync(cancellationToken);
            _logger.LogInformation("The exchange rates were refreshed.");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // An error here must not stop the app. The saved rates are still used, and the next refresh tries again.
            _logger.LogWarning(ex, "The background refresh of the exchange rates failed.");
        }
    }
}
