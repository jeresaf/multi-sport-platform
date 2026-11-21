using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Venues.ChangeVenueStatus;

/// <summary>
/// Moves a venue along its lifecycle (S07 D12): the status-history row and the audit entry (with the reason) commit
/// together. A move the current status doesn't allow is <c>VENUE_STATUS_INVALID</c>; unarchiving goes back only to the
/// status before archiving (Q55). The version guards the move: a venue changed since the read is a conflict.
/// </summary>
public sealed class ChangeVenueStatusHandler(RegistryDbContext context, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<Result> HandleAsync(Guid venueId, ChangeVenueStatusRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ifMatch);
        var reason = request.Reason!.Trim();
        audit.Describe(VenuesEndpoints.EntityType, venueId, reason);

        // The history is read whole: unarchiving needs the status before archiving, and a venue has few rows.
        var venue = await context.Set<Venue>().Include(v => v.StatusHistory).SingleOrDefaultAsync(v => v.Id == venueId, cancellationToken);
        if (venue is null)
        {
            return Result.Failure(RegistryErrors.VenueNotFound);
        }

        if (!RowVersion.IsSatisfied(context.Entry(venue).Property<uint>(AuditColumns.Version).CurrentValue, request.Version, ifMatch))
        {
            return Result.Failure(RegistryErrors.VenueConcurrencyConflict);
        }

        var changed = venue.ChangeStatus(request.Status!.Value, reason, actor, timeProvider.GetUtcNow());
        if (changed.IsFailure)
        {
            return changed;
        }

        return await context.SaveChangesResultAsync(cancellationToken);
    }
}
