using System.Diagnostics;
using SportsPlatform.Api.Middleware;

namespace SportsPlatform.Api.Services;

public sealed record ErrorReportContext(
    string CorrelationId,
    string RequestId,
    string? TraceId,
    string Method,
    string Path,
    string? TenantId,
    string? UserId);

public interface IErrorReporter
{
    Task ReportAsync(
        Exception exception,
        ErrorReportContext context,
        CancellationToken cancellationToken = default);
}

public sealed class LoggingErrorReporter : IErrorReporter
{
    private readonly ILogger<LoggingErrorReporter> _logger;

    public LoggingErrorReporter(ILogger<LoggingErrorReporter> logger) => _logger = logger;

    public Task ReportAsync(
        Exception exception,
        ErrorReportContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogError(
            exception,
            "Unhandled request error for {Method} {Path} with correlation {CorrelationId}, request {RequestId}, trace {TraceId}, tenant {TenantId}, and user {UserId}",
            context.Method,
            context.Path,
            context.CorrelationId,
            context.RequestId,
            context.TraceId,
            context.TenantId,
            context.UserId);
        return Task.CompletedTask;
    }

    public static ErrorReportContext CreateContext(HttpContext context) => new(
        context.Items[CorrelationContextMiddleware.ItemName]?.ToString()
            ?? Activity.Current?.TraceId.ToString()
            ?? context.TraceIdentifier,
        context.TraceIdentifier,
        Activity.Current?.TraceId.ToString(),
        context.Request.Method,
        context.Request.Path.ToString(),
        context.Items["TenantId"]?.ToString(),
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value);
}
