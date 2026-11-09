using SmartExpenseTracker.Application.Models;

namespace SmartExpenseTracker.Application.Interfaces;

public interface IStatisticsService
{
    void RecordRequest(string endpoint, int statusCode, long elapsedMs);

    void RecordExchangeRateEvent(ExchangeRateEvent exchangeRateEvent);

    StatisticsDto GetSnapshot();
}
