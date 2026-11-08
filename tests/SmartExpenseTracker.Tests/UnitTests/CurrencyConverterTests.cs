using SmartExpenseTracker.Application.Exceptions;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Services;

namespace SmartExpenseTracker.Tests.UnitTests;

public class CurrencyConverterTests
{
    // A fake rates service with fixed rates: 1 EUR = 1.25 USD and 1 EUR = 160 JPY.
    private class FakeExchangeRateService : IExchangeRateService
    {
        public int Calls { get; private set; }

        public Task<Dictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new Dictionary<string, decimal> { ["USD"] = 1.25m, ["JPY"] = 160m });
        }
    }

    // Euros stay the same and we do not even ask for the rates.
    [Fact]
    public async Task ConvertToEurAsync_Euros_ReturnsTheSameAmountWithoutAskingTheRates()
    {
        var rates = new FakeExchangeRateService();
        var converter = new CurrencyConverter(rates);

        var result = await converter.ConvertToEurAsync(42.5m, "EUR", CancellationToken.None);

        Assert.Equal(42.5m, result.AmountInEur);
        Assert.Equal(1m, result.ExchangeRate);
        Assert.Equal(0, rates.Calls);
    }

    // 100 USD with 1 EUR = 1.25 USD gives 80 EUR, and the rate is 0.8.
    [Fact]
    public async Task ConvertToEurAsync_Dollars_DividesByTheRate()
    {
        var converter = new CurrencyConverter(new FakeExchangeRateService());

        var result = await converter.ConvertToEurAsync(100m, "USD", CancellationToken.None);

        Assert.Equal(80m, result.AmountInEur);
        Assert.Equal(0.8m, result.ExchangeRate);
    }

    // The currency code can be written in lower case and with spaces.
    [Fact]
    public async Task ConvertToEurAsync_LowerCaseCodeWithSpaces_Works()
    {
        var converter = new CurrencyConverter(new FakeExchangeRateService());

        var result = await converter.ConvertToEurAsync(100m, " usd ", CancellationToken.None);

        Assert.Equal(80m, result.AmountInEur);
    }

    // The amount in euros is rounded to 2 decimals: 1234.56 JPY = 7.716 EUR, which becomes 7.72.
    [Fact]
    public async Task ConvertToEurAsync_RoundsTheAmountToTwoDecimals()
    {
        var converter = new CurrencyConverter(new FakeExchangeRateService());

        var result = await converter.ConvertToEurAsync(1234.56m, "JPY", CancellationToken.None);

        Assert.Equal(7.72m, result.AmountInEur);
        Assert.Equal(0.00625m, result.ExchangeRate);
    }

    // A currency that is not in the rates gives an error.
    [Fact]
    public async Task ConvertToEurAsync_UnknownCurrency_ThrowsUnsupportedCurrencyException()
    {
        var converter = new CurrencyConverter(new FakeExchangeRateService());

        await Assert.ThrowsAsync<UnsupportedCurrencyException>(
            () => converter.ConvertToEurAsync(10m, "XXX", CancellationToken.None));
    }
}
