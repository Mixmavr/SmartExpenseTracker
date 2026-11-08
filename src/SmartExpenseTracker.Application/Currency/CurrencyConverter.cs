namespace SmartExpenseTracker.Application.Currency;

public class CurrencyConverter : ICurrencyConverter
{
    private readonly IExchangeRateService _exchangeRateService;

    public CurrencyConverter(IExchangeRateService exchangeRateService)
    {
        _exchangeRateService = exchangeRateService;
    }

    public async Task<ConversionResult> ConvertToEurAsync(decimal amount, string currency, CancellationToken cancellationToken)
    {
        var code = currency.Trim().ToUpperInvariant();

        // Euros need no conversion
        if (code == ExchangeRateService.BaseCurrency)
        {
            return new ConversionResult(amount, 1m);
        }

        var rates = await _exchangeRateService.GetRatesAsync(cancellationToken);
        if (!rates.TryGetValue(code, out var unitsPerEur) || unitsPerEur <= 0)
        {
            throw new UnsupportedCurrencyException(code);
        }

        // The rates say "how many units of this currency make 1 EUR". We need the opposite.
        var exchangeRate = Math.Round(1m / unitsPerEur, 8, MidpointRounding.AwayFromZero);
        var amountInEur = Math.Round(amount * exchangeRate, 2, MidpointRounding.AwayFromZero);

        return new ConversionResult(amountInEur, exchangeRate);
    }
}
