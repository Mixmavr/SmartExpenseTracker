namespace SmartExpenseTracker.Application.Models;

public record ExchangeRateStatisticsDto(long CacheHits, long ProviderCalls, long ProviderFailures, long FallbackUses);
