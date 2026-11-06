namespace SmartExpenseTracker.Application.ExchangeRates;

public interface IExchangeRateProvider
{
    Task<Dictionary<string, decimal>> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken);
}