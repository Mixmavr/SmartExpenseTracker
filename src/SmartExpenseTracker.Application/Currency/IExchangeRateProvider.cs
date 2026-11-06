namespace SmartExpenseTracker.Application.Currency;

public interface IExchangeRateProvider
{
    Task<Dictionary<string, decimal>> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken);
}