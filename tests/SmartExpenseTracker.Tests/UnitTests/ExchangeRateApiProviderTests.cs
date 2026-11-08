using Microsoft.Extensions.Options;
using SmartExpenseTracker.Application.Exceptions;
using SmartExpenseTracker.Infrastructure.CurrencyProviders;

namespace SmartExpenseTracker.Tests.UnitTests;

public class ExchangeRateApiProviderTests
{
    // A fake HTTP layer that always "times out", like a real HttpClient does.
    private class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new TaskCanceledException("The request timed out.");
        }
    }

    private static ExchangeRateApiProvider CreateProvider(HttpMessageHandler handler)
    {
        var settings = Options.Create(new ExchangeRateApiSettings { BaseUrl = "https://example.test/", ApiKey = "key" });
        return new ExchangeRateApiProvider(new HttpClient(handler), settings);
    }

    // A timeout from the HTTP client becomes ExchangeRateProviderException (which the API maps to 503).
    [Fact]
    public async Task GetLatestRatesAsync_WhenRequestTimesOut_ThrowsProviderException()
    {
        var provider = CreateProvider(new TimeoutHandler());

        await Assert.ThrowsAsync<ExchangeRateProviderException>(
            () => provider.GetLatestRatesAsync("EUR", CancellationToken.None));
    }

    // If the caller cancels on purpose, the cancellation is NOT hidden as a provider error.
    [Fact]
    public async Task GetLatestRatesAsync_WhenCallerCancels_ThrowsOperationCanceled()
    {
        var provider = CreateProvider(new TimeoutHandler());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetLatestRatesAsync("EUR", cts.Token));
    }
}
