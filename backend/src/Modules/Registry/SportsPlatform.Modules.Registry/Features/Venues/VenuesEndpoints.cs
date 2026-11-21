using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Venues;

/// <summary><c>/api/admin/v1/venues</c> (S07 D19): the venues of the request's tenant (<c>X-Tenant-Id</c>).</summary>
internal static class VenuesEndpoints
{
    public const string Tag = "Venues";

    /// <summary>The audit entity type of a venue.</summary>
    public const string EntityType = "Venue";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder admin) => admin.MapGroup("/venues").WithTags(Tag);

    public static string PathOf(Guid venueId) => $"/api/admin/v1/venues/{venueId}";
}

/// <summary>What every change to the tenant's venues shares (S07 D21).</summary>
internal static class VenueChanges
{
    /// <summary>
    /// Holds <c>registry.venues:&lt;tenantId&gt;</c> until the transaction ends: creates (and changes to a compared
    /// field) queue on it while they check slugs and duplicates and save.
    /// </summary>
    public static async Task LockVenuesAsync(RegistryDbContext context, Guid tenantId, CancellationToken cancellationToken)
    {
        var key = $"registry.venues:{tenantId}";
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}
