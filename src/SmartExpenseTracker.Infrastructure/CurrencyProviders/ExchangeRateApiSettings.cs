namespace SmartExpenseTracker.Infrastructure.CurrencyProviders;

public class ExchangeRateApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
