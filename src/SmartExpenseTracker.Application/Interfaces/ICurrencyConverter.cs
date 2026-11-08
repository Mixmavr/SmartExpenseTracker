using SmartExpenseTracker.Application.Models;

namespace SmartExpenseTracker.Application.Interfaces;

public interface ICurrencyConverter
{
    Task<ConversionResult> ConvertToEurAsync(decimal amount, string currency, CancellationToken cancellationToken);
}
