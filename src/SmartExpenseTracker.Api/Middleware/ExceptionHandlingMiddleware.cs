using Microsoft.AspNetCore.Mvc;
using SmartExpenseTracker.Application.Exceptions;

namespace SmartExpenseTracker.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(context, ex);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception ex)
    {
        var (status, title) = ex switch
        {
            AppException app => app.Type switch
            {
                ErrorType.Validation => (StatusCodes.Status400BadRequest, "Bad request"),
                ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not found"),
                ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
                ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
            },
            ExchangeRateProviderException => (StatusCodes.Status503ServiceUnavailable, "Exchange rates are not available right now"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
        };

        // Known errors are explained to the user. For unexpected ones we show nothing about the inside of the app.
        string? detail = null;
        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(ex, "Unhandled exception");
        }
        else
        {
            _logger.LogWarning("Request failed with {Status}: {Message}", status, ex.Message);
            detail = ex.Message;
        }

        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
