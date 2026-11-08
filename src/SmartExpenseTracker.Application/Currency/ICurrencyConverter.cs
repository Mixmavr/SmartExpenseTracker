namespace SmartExpenseTracker.Application.Currency;

public interface ICurrencyConverter
{
    Task<ConversionResult> ConvertToEurAsync(decimal amount, string currency, CancellationToken cancellationToken);
}
