namespace SmartExpenseTracker.Application.Models;

public record StatisticsDto(
    DateTime Since,
    long TotalRequests,
    Dictionary<string, long> ByStatus,
    List<EndpointStatisticsDto> Endpoints,
    ExchangeRateStatisticsDto ExchangeRates);
