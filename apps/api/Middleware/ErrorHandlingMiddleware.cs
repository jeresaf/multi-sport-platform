using System.Net;
using System.Text.Json;
using FluentValidation;
using SportsPlatform.Api.Services;

namespace SportsPlatform.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }
            _logger.LogInformation(
                "Request was canceled by the client: {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
        }
        catch (ValidationException ex)
        {
            await HandleValidationExceptionAsync(context, ex);
        }
        catch (ArgumentException ex)
        {
            await HandleArgumentExceptionAsync(context, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            await HandleUnauthorizedAccessExceptionAsync(context, ex);
        }
        catch (InvalidOperationException ex)
        {
            await HandleInvalidOperationExceptionAsync(context, ex);
        }
        catch (KeyNotFoundException ex)
        {
            await HandleKeyNotFoundExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            await HandleInternalServerErrorAsync(context, ex);
        }
    }

    private async Task HandleValidationExceptionAsync(HttpContext context, ValidationException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

        var errors = exception.Errors.Select(e => new
        {
            field = e.PropertyName,
            message = e.ErrorMessage
        });

        var response = new
        {
            status = 400,
            title = "Validation Error",
            errors
        };

        _logger.LogWarning("Validation error: {Errors}", JsonSerializer.Serialize(errors));
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private async Task HandleUnauthorizedAccessExceptionAsync(HttpContext context, UnauthorizedAccessException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;

        var response = new
        {
            status = 403,
            title = "Forbidden",
            detail = exception.Message
        };

        _logger.LogWarning("Unauthorized access: {Message}", exception.Message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private async Task HandleArgumentExceptionAsync(HttpContext context, ArgumentException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        var response = new
        {
            status = 400,
            title = "Validation Error",
            detail = exception.Message,
            field = exception.ParamName
        };
        _logger.LogWarning("Invalid request value for {Field}: {Message}", exception.ParamName, exception.Message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private async Task HandleInvalidOperationExceptionAsync(HttpContext context, InvalidOperationException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.Conflict;

        var response = new
        {
            status = 409,
            title = "Conflict",
            detail = exception.Message
        };

        _logger.LogWarning("Invalid operation: {Message}", exception.Message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private async Task HandleKeyNotFoundExceptionAsync(HttpContext context, KeyNotFoundException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;

        var response = new
        {
            status = 404,
            title = "Not Found",
            detail = exception.Message
        };

        _logger.LogWarning("Resource not found: {Message}", exception.Message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private async Task HandleInternalServerErrorAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var reporter = context.RequestServices.GetRequiredService<IErrorReporter>();
        try
        {
            await reporter.ReportAsync(
                exception,
                LoggingErrorReporter.CreateContext(context),
                CancellationToken.None);
        }
        catch (Exception reporterException)
        {
            _logger.LogError(
                reporterException,
                "Error reporter failed while processing an unhandled {ExceptionType}",
                exception.GetType().Name);
        }

        var response = new
        {
            status = 500,
            title = "Internal Server Error",
            detail = context.RequestServices.GetRequiredService<IWebHostEnvironment>() is var environment
                && (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
                ? exception.Message
                : "An unexpected error occurred. Please try again later.",
            correlationId = context.Items[CorrelationContextMiddleware.ItemName]?.ToString(),
            traceId = System.Diagnostics.Activity.Current?.TraceId.ToString()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

public static class ErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlingMiddleware>();
    }
}
