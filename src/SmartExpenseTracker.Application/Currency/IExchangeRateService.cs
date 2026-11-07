namespace SmartExpenseTracker.Application.Currency;

public interface IExchangeRateService
{
    Task<Dictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken);
}
