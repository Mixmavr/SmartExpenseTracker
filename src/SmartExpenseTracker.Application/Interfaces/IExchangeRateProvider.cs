namespace SmartExpenseTracker.Application.Interfaces;

public interface IExchangeRateProvider
{
    Task<Dictionary<string, decimal>> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken);
}