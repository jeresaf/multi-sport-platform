using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Registry.Contracts.Venues;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Infrastructure;
using DomainStatus = SportsPlatform.Modules.Registry.Domain.Venues.VenueStatus;
using VenueStatus = SportsPlatform.Modules.Registry.Contracts.Venues.VenueStatus;

namespace SportsPlatform.Modules.Registry.Features.Venues;

/// <summary><see cref="IVenueDirectory"/> over the scope's tenant (query filter and RLS).</summary>
public sealed class VenueDirectory(RegistryDbContext context) : IVenueDirectory
{
    public async Task<IReadOnlyList<VenueSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> venueIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(venueIds);
        if (venueIds.Count == 0)
        {
            return [];
        }

        var ids = venueIds.Distinct().ToArray();
        var rows = await context.Set<Venue>().AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Select(v => new { v.Id, v.Name, v.ShortName, v.TimeZone, v.City, v.Country, v.SupportedVariants, v.Status })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => new VenueSummary(
            row.Id, row.Name, row.ShortName, row.TimeZone, row.City, row.Country, [.. row.SupportedVariants], Map(row.Status)))];
    }

    private static VenueStatus Map(DomainStatus status) => status switch
    {
        DomainStatus.Active => VenueStatus.Active,
        DomainStatus.TemporarilyUnavailable => VenueStatus.TemporarilyUnavailable,
        DomainStatus.Closed => VenueStatus.Closed,
        DomainStatus.Archived => VenueStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
