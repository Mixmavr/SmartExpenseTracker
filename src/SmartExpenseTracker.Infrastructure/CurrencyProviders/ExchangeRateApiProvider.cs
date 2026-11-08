using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartExpenseTracker.Application.Exceptions;
using SmartExpenseTracker.Application.Interfaces;

namespace SmartExpenseTracker.Infrastructure.CurrencyProviders;

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
            using var httpResponse = await _httpClient.GetAsync(url, cancellationToken);
            response = await httpResponse.Content.ReadFromJsonAsync<ExchangeRateApiResponse>(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ExchangeRateProviderException("Could not reach ExchangeRate-API.", ex);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new ExchangeRateProviderException("ExchangeRate-API returned a response we could not read.", ex);
        }

        if (response == null || response.Result != "success")
        {
            throw new ExchangeRateProviderException($"ExchangeRate-API returned an error: {response?.ErrorType ?? "unknown"}.");
        }

        return response.ConversionRates;
    }
}
