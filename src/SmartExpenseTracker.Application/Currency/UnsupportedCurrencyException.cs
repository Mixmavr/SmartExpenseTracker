namespace SmartExpenseTracker.Application.Currency;

public class UnsupportedCurrencyException : Exception
{
    public UnsupportedCurrencyException(string currency) : base($"The currency '{currency}' is not supported.")
    {
    }
}
