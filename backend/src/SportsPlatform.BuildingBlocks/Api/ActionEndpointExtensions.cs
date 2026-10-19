using Microsoft.AspNetCore.Builder;
using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>
/// Every endpoint declares exactly one of these (docs/architecture/authorization.md). The Host refuses to start
/// otherwise, and its authorization middleware enforces the declared action.
/// </summary>
public static class ActionEndpointExtensions
{
    public static TBuilder RequireAction<TBuilder>(this TBuilder builder, ActionDefinition action)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(action);
        return builder.WithMetadata(new RequiresActionMetadata(action));
    }

    /// <summary>Public reads and infrastructure endpoints; not allowed under <c>/api/admin</c> or <c>/api/platform</c>.</summary>
    public static TBuilder AllowPublic<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(AllowPublicMetadata.Instance);
    }

    /// <summary>
    /// Platform endpoints under <c>/tenants/{tenantId}</c>: the Host sets the tenant context to the route's tenant
    /// before authorization, or answers 404 for an unknown tenant.
    /// </summary>
    public static TBuilder RunsUnderRouteTenant<TBuilder>(this TBuilder builder, string routeValueName = "tenantId")
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeValueName);
        return builder.WithMetadata(new RunsUnderRouteTenantMetadata(routeValueName));
    }

    /// <summary>
    /// Public endpoints that serve one tenant's data (S02 D6): the Host resolves the tenant from the request host, or
    /// answers <c>404 TENANT_NOT_FOUND</c> or <c>403 TENANT_UNAVAILABLE</c>. Only under <c>/api/public</c>.
    /// </summary>
    public static TBuilder RequirePublicTenant<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(RequiresPublicTenantMetadata.Instance);
    }
}
