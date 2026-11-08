namespace SmartExpenseTracker.Application.Exceptions;

public class UnsupportedCurrencyException : Exception
{
    public UnsupportedCurrencyException(string currency) : base($"The currency '{currency}' is not supported.")
    {
    }
}
