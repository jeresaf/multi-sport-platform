using System.Text.RegularExpressions;
using SportsPlatform.BuildingBlocks.Messaging;

namespace SportsPlatform.Host.Api;

/// <summary>
/// Gives every request a correlation ID: a well-formed incoming <c>X-Correlation-Id</c> or a new UUIDv7. The ID becomes
/// <see cref="HttpContext.TraceIdentifier"/> and the request's <see cref="CorrelationContext"/> (so outbox messages carry it),
/// is echoed in the response header and is added to the logging scope.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, TimeProvider timeProvider, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public const string LogProperty = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = context.Request.Headers[HeaderName] is [{ } incoming] && Format().IsMatch(incoming)
            ? incoming
            : Guid.CreateVersion7(timeProvider.GetUtcNow()).ToString();

        context.TraceIdentifier = correlationId;
        context.RequestServices.GetService<CorrelationContext>()?.Set(correlationId, causationId: null);
        // OnStarting survives the exception handler clearing the response, so error responses carry the header too.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { [LogProperty] = correlationId }))
        {
            await next(context);
        }
    }

    // Client-supplied IDs end up in logs and headers: short, and only characters that need no escaping.
    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,64}\z")]
    private static partial Regex Format();
}
