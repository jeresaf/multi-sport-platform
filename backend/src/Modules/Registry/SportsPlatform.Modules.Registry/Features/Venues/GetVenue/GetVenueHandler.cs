using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Venues.GetVenue;

/// <summary>One venue of the request's tenant, whatever its status; another tenant's venue is not found (RLS).</summary>
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

        var v = venue.Venue;
        return Result.Success(new VenueResponse(
            v.Id, v.Name, v.ShortName, v.Slug, v.Type, v.AddressLine, v.City, v.Region, v.Country, v.Latitude, v.Longitude,
            v.TimeZone, v.Capacity, v.Surfaces, v.SupportedVariants, v.Status, venue.Version));
    }
}
