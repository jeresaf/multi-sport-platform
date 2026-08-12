using System.Diagnostics;
using Serilog.Context;

namespace SportsPlatform.Api.Middleware;

public sealed class CorrelationContextMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemName = "CorrelationId";
    private const int MaxCorrelationIdLength = 128;
    private readonly RequestDelegate _next;

    public CorrelationContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsValid(supplied)
            ? supplied!
            : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

        context.Items[ItemName] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);
        Activity.Current?.AddBaggage("correlation.id", correlationId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
        using (LogContext.PushProperty("TraceId", Activity.Current?.TraceId.ToString() ?? string.Empty))
            await _next(context);
    }

    private static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaxCorrelationIdLength
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '.');
}

public static class CorrelationContextMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationContext(this IApplicationBuilder builder) =>
        builder.UseMiddleware<CorrelationContextMiddleware>();
}
