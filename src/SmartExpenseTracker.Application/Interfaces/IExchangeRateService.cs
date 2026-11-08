namespace SmartExpenseTracker.Application.Interfaces;

public interface IExchangeRateService
{
    Task<Dictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken);
}
