using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Venues.GetVenue;

/// <summary>
/// One venue of the request's tenant, whatever its status, with its current aliases and its external identifiers;
/// another tenant's venue is not found (RLS).
/// </summary>
public sealed class GetVenueHandler(RegistryDbContext context)
{
    public async Task<Result<VenueResponse>> HandleAsync(Guid venueId, CancellationToken cancellationToken)
    {
        var venue = await context.Set<Venue>().AsNoTracking()
            .Where(v => v.Id == venueId)
            .Select(v => new { Venue = v, Version = EF.Property<uint>(v, AuditColumns.Version) })
            .SingleOrDefaultAsync(cancellationToken);
        if (venue is null)
        {
            return RegistryErrors.VenueNotFound;
        }

        var aliases = await context.Set<VenueAlias>().AsNoTracking()
            .Where(a => a.VenueId == venueId && a.RemovedAt == null)
            .OrderBy(a => a.Id)
            .Select(a => new VenueAliasResponse(a.Id, a.Name, a.Kind))
            .ToListAsync(cancellationToken);

        var identifiers = await context.Set<ExternalIdentifier>().AsNoTracking()
            .Where(i => i.EntityType == ExternalIdentifierEntityType.Venue && i.EntityId == venueId)
            .OrderBy(i => i.Authority).ThenBy(i => i.Id)
            .Select(i => new ExternalIdentifierResponse(i.Id, i.Authority, i.Value, i.ValidFrom, i.ValidTo))
            .ToListAsync(cancellationToken);

        var v = venue.Venue;
        return Result.Success(new VenueResponse(
            v.Id, v.Name, v.ShortName, v.Slug, v.Type, v.AddressLine, v.City, v.Region, v.Country, v.Latitude, v.Longitude,
            v.TimeZone, v.Capacity, v.Surfaces, v.SupportedVariants, v.Status, aliases, identifiers, venue.Version));
    }
}
