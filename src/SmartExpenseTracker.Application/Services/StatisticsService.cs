using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Models;

namespace SmartExpenseTracker.Application.Services;

public class StatisticsService : IStatisticsService
{
    // Only one thread at a time can be inside a lock (_lock) block, so no count is ever lost.
    private readonly object _lock = new();

    private readonly DateTime _startedAt = DateTime.UtcNow;

    private long _totalRequests;
    private readonly Dictionary<string, long> _byStatus = new();
    private readonly Dictionary<string, EndpointCounter> _endpoints = new();

    private long _cacheHits;
    private long _providerCalls;
    private long _providerFailures;
    private long _fallbackUses;

    private class EndpointCounter
    {
        public long Count;
        public long TotalMs;
    }

    public void RecordRequest(string endpoint, int statusCode, long elapsedMs)
    {
        lock (_lock)
        {
            _totalRequests++;

            var group = statusCode / 100;
            if (group is >= 1 and <= 5)
            {
                var key = $"{group}xx";
                _byStatus[key] = _byStatus.GetValueOrDefault(key) + 1;
            }

            if (!_endpoints.TryGetValue(endpoint, out var counter))
            {
                counter = new EndpointCounter();
                _endpoints[endpoint] = counter;
            }

            counter.Count++;
            counter.TotalMs += elapsedMs;
        }
    }

    public void RecordExchangeRateEvent(ExchangeRateEvent exchangeRateEvent)
    {
        lock (_lock)
        {
            switch (exchangeRateEvent)
            {
                case ExchangeRateEvent.CacheHit:
                    _cacheHits++;
                    break;
                case ExchangeRateEvent.ProviderCall:
                    _providerCalls++;
                    break;
                case ExchangeRateEvent.ProviderFailure:
                    _providerFailures++;
                    break;
                case ExchangeRateEvent.FallbackUsed:
                    _fallbackUses++;
                    break;
            }
        }
    }

    // Everything is read inside the lock, so all the numbers are from the same moment.
    public StatisticsDto GetSnapshot()
    {
        lock (_lock)
        {
            var byStatus = _byStatus.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);

            var endpoints = _endpoints
                .Select(pair => new EndpointStatisticsDto(pair.Key, pair.Value.Count, Math.Round((double)pair.Value.TotalMs / pair.Value.Count, 1)))
                .OrderByDescending(e => e.Count)
                .ThenBy(e => e.Endpoint)
                .ToList();

            var exchangeRates = new ExchangeRateStatisticsDto(_cacheHits, _providerCalls, _providerFailures, _fallbackUses);

            return new StatisticsDto(_startedAt, _totalRequests, byStatus, endpoints, exchangeRates);
        }
    }
}
