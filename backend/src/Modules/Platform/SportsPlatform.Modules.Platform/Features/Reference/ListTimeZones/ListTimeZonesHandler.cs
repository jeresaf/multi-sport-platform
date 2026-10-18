using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Platform.Domain.Reference;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.Modules.Platform.Features.Reference.ListTimeZones;

/// <summary>An IANA time zone: its ID as <c>code</c> (<c>Africa/Kampala</c>) and its country; no country for <c>Etc/UTC</c>.</summary>
public sealed record TimeZoneResponse(string Code, string? CountryCode);

public sealed record ListTimeZonesResponse(IReadOnlyList<TimeZoneResponse> Items);

/// <summary>Active time zones, ordered by code; only one country's zones when one is given.</summary>
public sealed class ListTimeZonesHandler(PlatformDbContext context)
{
    public async Task<ListTimeZonesResponse> HandleAsync(string? country, CancellationToken cancellationToken)
    {
        var zones = context.Set<IanaTimeZone>().AsNoTracking().Where(z => z.IsActive);
        if (country is not null)
        {
            zones = zones.Where(z => z.CountryCode == country);
        }

        var items = await zones.Select(z => new TimeZoneResponse(z.Code, z.CountryCode)).ToListAsync(cancellationToken);
        return new(items.OrderBy(z => z.Code, StringComparer.Ordinal).ToList());
    }
}
