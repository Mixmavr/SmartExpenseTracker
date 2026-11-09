namespace SmartExpenseTracker.Application.Interfaces;

public interface IExchangeRateService
{
    Task<Dictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken);

    // Always asks the API and replaces the saved rates. Used by the background service.
    Task RefreshAsync(CancellationToken cancellationToken);
}
