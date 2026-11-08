using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SmartExpenseTracker.Api.Middleware;
using SmartExpenseTracker.Application.Exceptions;

namespace SmartExpenseTracker.Tests.UnitTests;

public class ExceptionHandlingMiddlewareTests
{
    // Runs the middleware with a "next step" that fails with the given error, and returns what the user would get.
    private static async Task<(int Status, JsonElement Body)> RunAsync(Exception? error)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => error == null ? Task.CompletedTask : throw error,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = context.Response.Body.Length == 0
            ? default
            : await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);

        return (context.Response.StatusCode, body);
    }

    // When nothing fails, the middleware does not touch the response.
    [Fact]
    public async Task InvokeAsync_NoError_LeavesTheResponseAlone()
    {
        var (status, _) = await RunAsync(null);

        Assert.Equal(StatusCodes.Status200OK, status);
    }

    // A validation error is the user's mistake: 400 and a message that explains it.
    [Fact]
    public async Task InvokeAsync_ValidationError_Returns400WithTheMessage()
    {
        var (status, body) = await RunAsync(new AppException(ErrorType.Validation, "The currency 'XXX' is not supported."));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Contains("XXX", body.GetProperty("detail").GetString());
    }

    // A conflict with data that already exists: 409 and a message that explains it.
    [Fact]
    public async Task InvokeAsync_Conflict_Returns409WithTheMessage()
    {
        var (status, body) = await RunAsync(new AppException(ErrorType.Conflict, "The email 'jane@example.com' is already registered."));

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Contains("jane@example.com", body.GetProperty("detail").GetString());
    }

    // Wrong login details: 401.
    [Fact]
    public async Task InvokeAsync_Unauthorized_Returns401()
    {
        var (status, body) = await RunAsync(new AppException(ErrorType.Unauthorized, "Invalid email or password."));

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.Equal("Invalid email or password.", body.GetProperty("detail").GetString());
    }

    // When the rates API is down and we have no backup, the service is temporarily unavailable: 503.
    [Fact]
    public async Task InvokeAsync_ExchangeRateProviderFails_Returns503()
    {
        var (status, _) = await RunAsync(new ExchangeRateProviderException("The API is down."));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
    }

    // An unexpected error gives 500 and does NOT reveal its message to the user.
    [Fact]
    public async Task InvokeAsync_UnexpectedError_Returns500WithoutTheInternalMessage()
    {
        var (status, body) = await RunAsync(new InvalidOperationException("secret internal detail"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.DoesNotContain("secret internal detail", body.GetRawText());
    }
}
