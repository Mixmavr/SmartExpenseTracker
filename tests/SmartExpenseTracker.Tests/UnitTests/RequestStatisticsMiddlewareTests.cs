using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging.Abstractions;
using SmartExpenseTracker.Api.Middleware;
using SmartExpenseTracker.Application.Exceptions;
using SmartExpenseTracker.Application.Services;

namespace SmartExpenseTracker.Tests.UnitTests;

public class RequestStatisticsMiddlewareTests
{
    // The endpoint is the route pattern, not the real path, so different ids count together.
    [Fact]
    public async Task InvokeAsync_UsesTheRoutePatternAsTheEndpointName()
    {
        var statistics = new StatisticsService();
        var middleware = new RequestStatisticsMiddleware(_ => Task.CompletedTask, statistics);

        foreach (var id in new[] { 5, 57 })
        {
            var context = new DefaultHttpContext();
            context.Request.Method = "GET";
            context.Request.Path = $"/api/expenses/{id}";
            context.SetEndpoint(new RouteEndpoint(
                _ => Task.CompletedTask, RoutePatternFactory.Parse("api/expenses/{id:int}"), 0, EndpointMetadataCollection.Empty, "test"));

            await middleware.InvokeAsync(context);
        }

        var endpoint = Assert.Single(statistics.GetSnapshot().Endpoints);
        Assert.Equal("GET api/expenses/{id:int}", endpoint.Endpoint);
        Assert.Equal(2, endpoint.Count);
    }

    // A request that matches no endpoint (like an unknown URL) is counted as "unmatched".
    [Fact]
    public async Task InvokeAsync_NoEndpoint_IsCountedAsUnmatched()
    {
        var statistics = new StatisticsService();
        var middleware = new RequestStatisticsMiddleware(ctx => { ctx.Response.StatusCode = 404; return Task.CompletedTask; }, statistics);

        await middleware.InvokeAsync(new DefaultHttpContext());

        var snapshot = statistics.GetSnapshot();
        Assert.Equal("unmatched", Assert.Single(snapshot.Endpoints).Endpoint);
        Assert.Equal(1, snapshot.ByStatus["4xx"]);
    }

    // The Swagger UI files are not counted, but the request still goes on to the next step.
    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/Swagger/swagger-ui.css")]
    public async Task InvokeAsync_SwaggerFiles_AreNotCounted(string path)
    {
        var statistics = new StatisticsService();
        var nextWasCalled = false;
        var middleware = new RequestStatisticsMiddleware(_ => { nextWasCalled = true; return Task.CompletedTask; }, statistics);
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        Assert.True(nextWasCalled);
        Assert.Equal(0, statistics.GetSnapshot().TotalRequests);
    }

    // Only the /swagger folder is skipped: a different URL that starts with the same letters is still counted.
    [Fact]
    public async Task InvokeAsync_UrlThatOnlyStartsLikeSwagger_IsStillCounted()
    {
        var statistics = new StatisticsService();
        var middleware = new RequestStatisticsMiddleware(ctx => { ctx.Response.StatusCode = 404; return Task.CompletedTask; }, statistics);
        var context = new DefaultHttpContext();
        context.Request.Path = "/swaggerish";

        await middleware.InvokeAsync(context);

        Assert.Equal(1, statistics.GetSnapshot().TotalRequests);
    }

    // An error handled by the error middleware (placed inside this one) is counted with its final status, 500.
    [Fact]
    public async Task InvokeAsync_ErrorHandledByTheNextMiddleware_IsCountedWithItsStatus()
    {
        var statistics = new StatisticsService();
        var errorMiddleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("boom"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var middleware = new RequestStatisticsMiddleware(errorMiddleware.InvokeAsync, statistics);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context);

        Assert.Equal(1, statistics.GetSnapshot().ByStatus["5xx"]);
    }

    // A known error (like a missing resource) is counted as a 4xx.
    [Fact]
    public async Task InvokeAsync_NotFoundError_IsCountedAs4xx()
    {
        var statistics = new StatisticsService();
        var errorMiddleware = new ExceptionHandlingMiddleware(
            _ => throw new AppException(ErrorType.NotFound, "Not here."),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var middleware = new RequestStatisticsMiddleware(errorMiddleware.InvokeAsync, statistics);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context);

        Assert.Equal(1, statistics.GetSnapshot().ByStatus["4xx"]);
    }
}
