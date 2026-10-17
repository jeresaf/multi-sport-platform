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
}
