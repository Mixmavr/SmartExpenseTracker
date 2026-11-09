using SmartExpenseTracker.Application.Models;
using SmartExpenseTracker.Application.Services;

namespace SmartExpenseTracker.Tests.UnitTests;

public class StatisticsServiceTests
{
    // Requests are counted: in total, by status group, and per endpoint with the average time.
    [Fact]
    public void RecordRequest_CountsTheRequests()
    {
        var statistics = new StatisticsService();

        statistics.RecordRequest("GET api/expenses", 200, 10);
        statistics.RecordRequest("GET api/expenses", 200, 20);
        statistics.RecordRequest("POST api/expenses", 400, 5);
        statistics.RecordRequest("POST api/expenses", 503, 7);

        var snapshot = statistics.GetSnapshot();

        Assert.Equal(4, snapshot.TotalRequests);
        Assert.Equal(2, snapshot.ByStatus["2xx"]);
        Assert.Equal(1, snapshot.ByStatus["4xx"]);
        Assert.Equal(1, snapshot.ByStatus["5xx"]);
        Assert.False(snapshot.ByStatus.ContainsKey("3xx"));
    }

    // The endpoints are sorted with the most used first, and the average time is correct.
    [Fact]
    public void GetSnapshot_ListsTheEndpointsWithTheirAverageTime()
    {
        var statistics = new StatisticsService();
        statistics.RecordRequest("POST api/expenses", 201, 100);
        statistics.RecordRequest("GET api/expenses", 200, 10);
        statistics.RecordRequest("GET api/expenses", 200, 25);

        var endpoints = statistics.GetSnapshot().Endpoints;

        Assert.Equal(new EndpointStatisticsDto("GET api/expenses", 2, 17.5), endpoints[0]);
        Assert.Equal(new EndpointStatisticsDto("POST api/expenses", 1, 100), endpoints[1]);
    }

    // The exchange rate events are counted.
    [Fact]
    public void RecordExchangeRateEvent_CountsEachKind()
    {
        var statistics = new StatisticsService();

        statistics.RecordExchangeRateEvent(ExchangeRateEvent.CacheHit);
        statistics.RecordExchangeRateEvent(ExchangeRateEvent.CacheHit);
        statistics.RecordExchangeRateEvent(ExchangeRateEvent.ProviderCall);
        statistics.RecordExchangeRateEvent(ExchangeRateEvent.ProviderFailure);
        statistics.RecordExchangeRateEvent(ExchangeRateEvent.FallbackUsed);

        Assert.Equal(new ExchangeRateStatisticsDto(2, 1, 1, 1), statistics.GetSnapshot().ExchangeRates);
    }

    // A new service has all counters at zero.
    [Fact]
    public void GetSnapshot_NewService_IsEmpty()
    {
        var snapshot = new StatisticsService().GetSnapshot();

        Assert.Equal(0, snapshot.TotalRequests);
        Assert.Empty(snapshot.ByStatus);
        Assert.Empty(snapshot.Endpoints);
        Assert.Equal(new ExchangeRateStatisticsDto(0, 0, 0, 0), snapshot.ExchangeRates);
    }

    // Thread-safety: 16 threads start together and record 800,000 times. The totals must be exact, with no lost count.
    [Fact]
    public async Task RecordRequest_ManyThreadsAtTheSameTime_DoesNotLoseAnyCount()
    {
        const int threads = 16;
        const int recordsPerThread = 50_000;
        var statistics = new StatisticsService();
        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, threads)
            .Select(_ => Task.Factory.StartNew(() =>
            {
                start.Wait();
                for (var i = 0; i < recordsPerThread; i++)
                {
                    statistics.RecordRequest(i % 2 == 0 ? "GET api/a" : "GET api/b", i % 4 == 0 ? 404 : 200, 2);
                    statistics.RecordExchangeRateEvent(ExchangeRateEvent.CacheHit);
                }
            }, TaskCreationOptions.LongRunning))
            .ToArray();

        start.Set();
        await Task.WhenAll(tasks);

        var snapshot = statistics.GetSnapshot();

        Assert.Equal(800_000, snapshot.TotalRequests);
        Assert.Equal(600_000, snapshot.ByStatus["2xx"]);
        Assert.Equal(200_000, snapshot.ByStatus["4xx"]);
        Assert.All(snapshot.Endpoints, e => Assert.Equal(400_000, e.Count));
        Assert.All(snapshot.Endpoints, e => Assert.Equal(2, e.AverageMs));
        Assert.Equal(800_000, snapshot.ExchangeRates.CacheHits);
    }
}
