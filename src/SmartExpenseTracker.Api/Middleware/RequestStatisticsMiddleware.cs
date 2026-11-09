using System.Diagnostics;
using SmartExpenseTracker.Application.Interfaces;

namespace SmartExpenseTracker.Api.Middleware;

public class RequestStatisticsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IStatisticsService _statistics;

    public RequestStatisticsMiddleware(RequestDelegate next, IStatisticsService statistics)
    {
        _next = next;
        _statistics = statistics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // The Swagger UI files are not part of the API, so they are not counted.
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _statistics.RecordRequest(GetEndpointName(context), context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
    }

    // We use the route pattern (api/expenses/{id:int}) and not the real path (api/expenses/57), so the ids do not create endless entries.
    private static string GetEndpointName(HttpContext context)
    {
        var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;

        return pattern == null ? "unmatched" : $"{context.Request.Method} {pattern}";
    }
}
