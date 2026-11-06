using System.Text.Json.Serialization;

namespace SmartExpenseTracker.Infrastructure.CurrencyProviders;

public class ExchangeRateApiResponse
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("error-type")]
    public string? ErrorType { get; set; }

    [JsonPropertyName("conversion_rates")]
    public Dictionary<string, decimal> ConversionRates { get; set; } = new();
}
