namespace SmartExpenseTracker.Application.Models;

public enum ExchangeRateEvent
{
    CacheHit,
    ProviderCall,
    ProviderFailure,
    FallbackUsed,
}
