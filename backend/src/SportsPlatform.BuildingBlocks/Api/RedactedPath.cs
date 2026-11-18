using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>
/// Endpoint metadata: the request path carries a secret, such as a signed media URL's token (S06 D17). The request's
/// completion event, every event logged during the request once it is routed, and the trace record the route template (<c>/api/public/v1/media/{token}</c>) instead of the path.
/// </summary>
public sealed class RedactedPathMetadata
{
    public static readonly RedactedPathMetadata Instance = new();

    private RedactedPathMetadata()
    {
    }
}

public static class RedactedPath
{
    /// <summary>Marks the endpoint's path as secret: logs and traces show its route template.</summary>
    public static TBuilder RedactPathInTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(RedactedPathMetadata.Instance);
    }

    /// <summary>The route template to record instead of the path, if the request's endpoint is marked; else null.</summary>
    public static string? Of(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // After an unhandled exception the handler clears the endpoint; the exception feature keeps it.
        var endpoint = context.GetEndpoint() ?? context.Features.Get<IExceptionHandlerFeature>()?.Endpoint;
        return endpoint is RouteEndpoint route && route.Metadata.GetMetadata<RedactedPathMetadata>() is not null
            ? "/" + (route.RoutePattern.RawText ?? string.Empty).TrimStart('/')
            : null;
    }
}
