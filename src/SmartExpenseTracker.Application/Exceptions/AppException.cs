namespace SmartExpenseTracker.Application.Exceptions;

public class AppException : Exception
{
    public ErrorType Type { get; }

    public AppException(ErrorType type, string description) : base(description)
    {
        Type = type;
    }
}
