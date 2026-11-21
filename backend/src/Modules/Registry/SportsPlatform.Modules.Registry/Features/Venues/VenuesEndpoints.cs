using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Features.Venues.GetVenue;
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

/// <summary>
/// What every change to the tenant's venues shares (S07 D21): the lock, and, for the commands on a venue's aliases and
/// identifiers (S07-T1c), loading the venue at the version the client read and the answer (the venue with its
/// <c>ETag</c>).
/// </summary>
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

    /// <summary>
    /// The venue with its aliases, or <c>VENUE_NOT_FOUND</c>, or <c>VENUE_CONCURRENCY_CONFLICT</c> when
    /// <paramref name="version"/> or <paramref name="ifMatch"/> names another version.
    /// </summary>
    public static async Task<(Venue? Venue, FieldRefusal? Refusal)> LoadAsync(
        RegistryDbContext context, Guid venueId, uint? version, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        var venue = await context.Set<Venue>().Include(v => v.Aliases).SingleOrDefaultAsync(v => v.Id == venueId, cancellationToken);
        if (venue is null)
        {
            return (null, new(RegistryErrors.VenueNotFound));
        }

        return RowVersion.IsSatisfied(context.Entry(venue).Property<uint>(AuditColumns.Version).CurrentValue, version, ifMatch)
            ? (venue, null)
            : (null, new(RegistryErrors.VenueConcurrencyConflict));
    }

    /// <summary>Reads <c>If-Match</c>, runs <paramref name="change"/>, and answers the refusal or the saved venue.</summary>
    public static async Task<Results<Ok<VenueResponse>, ProblemHttpResult>> AnswerAsync(
        Guid venueId,
        GetVenueHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        Func<IfMatch, Task<FieldRefusal?>> change)
    {
        if (!RowVersion.TryReadIfMatch(httpRequest, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        if (await change(ifMatch) is { } refusal)
        {
            return refusal.ToProblem();
        }

        // Saved: the read is not cancelled with the request.
        var venue = await read.HandleAsync(venueId, CancellationToken.None);
        venue.Value.SetETag(response);
        return TypedResults.Ok(venue.Value);
    }
}
