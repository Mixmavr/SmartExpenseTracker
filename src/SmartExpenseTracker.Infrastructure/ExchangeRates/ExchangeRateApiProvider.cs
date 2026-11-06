using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SmartExpenseTracker.Application.ExchangeRates;

namespace SmartExpenseTracker.Infrastructure.ExchangeRates;

public class ExchangeRateApiProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ExchangeRateApiSettings _settings;

    public ExchangeRateApiProvider(HttpClient httpClient, IOptions<ExchangeRateApiSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<Dictionary<string, decimal>> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken)
    {
        var url = $"{_settings.BaseUrl}{_settings.ApiKey}/latest/{baseCurrency}";

        ExchangeRateApiResponse? response;
        try
        {
            response = await _httpClient.GetFromJsonAsync<ExchangeRateApiResponse>(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ExchangeRateProviderException("Could not reach ExchangeRate-API.", ex);
        }

        if (response == null || response.Result != "success")
        {
            throw new ExchangeRateProviderException($"ExchangeRate-API returned an error: {response?.ErrorType}");
        }

        return response.ConversionRates;
    }
}
