using Serilog.Core;
using Serilog.Events;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.Host.Observability;

/// <summary>
/// Adds the request's <c>CorrelationId</c>, <c>TenantId</c>, <c>ActorKind</c> and <c>ActorId</c> to every event logged
/// during a request. Values are read when the event is written, so a tenant or actor resolved later in the pipeline
/// (S02, S03) appears from that point on. <c>TenantId</c> is left out when the request has no tenant.
/// </summary>
/// <remarks>
/// <see cref="ITenantContext"/> and <see cref="ICurrentActor"/> implementations must not log while they are being
/// resolved: the enricher resolves them for every event.
/// </remarks>
public sealed class RequestContextEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public const string CorrelationId = "CorrelationId";
    public const string TenantId = "TenantId";
    public const string ActorKind = "ActorKind";
    public const string ActorId = "ActorId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        if (httpContextAccessor.HttpContext is not { } context)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(CorrelationId, context.TraceIdentifier));

        IServiceProvider services;
        try
        {
            services = context.RequestServices;
            if (services.GetService<ITenantContext>() is { TenantId: { } tenantId })
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(TenantId, tenantId));
            }

            if (services.GetService<ICurrentActor>() is { } actor)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ActorKind, actor.Kind.ToString()));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(ActorId, actor.ActorId));
            }
        }
        catch (ObjectDisposedException)
        {
            // Logged after the request's service scope ended: only the correlation ID is still known.
        }
    }
}
