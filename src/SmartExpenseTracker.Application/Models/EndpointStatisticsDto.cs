namespace SmartExpenseTracker.Application.Models;

public record EndpointStatisticsDto(string Endpoint, long Count, double AverageMs);
