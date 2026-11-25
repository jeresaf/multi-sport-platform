using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.People.ListPeople;
using SportsPlatform.Modules.Registry.Features.Venues.ListVenues;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Venues.PublicVenues;

/// <summary>
/// One venue of the request's public tenant (query filter and RLS) by ID or slug, in any status but <c>ARCHIVED</c>
/// (S07 D17).
/// </summary>
public sealed class GetPublicVenueHandler(RegistryDbContext context)
{
    public async Task<Result<PublicVenueResponse>> HandleAsync(string idOrSlug, CancellationToken cancellationToken)
    {
        var key = IdOrSlug.Parse(idOrSlug);
        if (!key.CanMatch)
        {
            return RegistryErrors.VenueNotFound;
        }

        var venues = context.Set<Venue>().AsNoTracking();
        var v = await (key.Id is { } id ? venues.Where(v => v.Id == id) : venues.Where(v => v.Slug == key.Slug))
            .Where(v => v.Status != VenueStatus.Archived)
            .SingleOrDefaultAsync(cancellationToken);
        if (v is null)
        {
            return RegistryErrors.VenueNotFound;
        }

        return Result.Success(new PublicVenueResponse(
            v.Id, v.Name, v.ShortName, v.Slug, v.Type, v.AddressLine, v.City, v.Region, v.Country, v.Latitude, v.Longitude,
            v.TimeZone, v.Capacity, v.Surfaces, v.SupportedVariants, v.Status));
    }
}

/// <summary>
/// The public venue directory of the request's public tenant (S07 D17), in the admin directory's order (sort key, ID),
/// with its items and cursor. Only one status at a time, <c>ACTIVE</c> by default, never <c>ARCHIVED</c>. <c>q</c>
/// matches only the name, as the admin directory does (D16: aliases and identifiers are <c>INTERNAL</c>).
/// </summary>
public sealed class ListPublicVenuesHandler(RegistryDbContext context)
{
    public async Task<ListPublicVenuesResponse> HandleAsync(PublicVenuesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var venues = context.Set<Venue>().AsNoTracking().Where(v => v.Status == query.Status);
        if (query.Variant is { } variant)
        {
            venues = venues.Where(v => v.SupportedVariants.Contains(variant));
        }

        if (query.Search is { } search)
        {
            venues = Matching(venues, search);
        }

        if (query.After is { } after)
        {
            venues = venues.Where(v =>
                v.SortKey.CompareTo(after.SortKey) > 0 || (v.SortKey == after.SortKey && v.Id.CompareTo(after.Id) > 0));
        }

        var rows = await venues
            .OrderBy(v => v.SortKey).ThenBy(v => v.Id)
            .Take(query.Limit + 1)
            .Select(v => new
            {
                Item = new VenueListItem(v.Id, v.Name, v.ShortName, v.Slug, v.Type, v.City, v.Country, v.SupportedVariants, v.Status),
                v.SortKey,
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > query.Limit;
        var page = rows.Take(query.Limit).ToList();
        var next = hasMore ? new DirectoryCursor(page[^1].SortKey, page[^1].Item.Id).Encode() : null;
        return new ListPublicVenuesResponse([.. page.Select(row => row.Item)], new PublicPageInfo(next, hasMore));
    }

    private static IQueryable<Venue> Matching(IQueryable<Venue> venues, string search)
    {
        // As the admin directory's, without the INTERNAL parts. Text without a letter or a digit has no key and matches
        // nothing (the admin directory's identifiers aren't public).
        var prefix = Venue.SortKeyOf(search);
        if (prefix.Length == 0)
        {
            return venues.Where(_ => false);
        }

        var key = Venue.NameKeyOf(search);
        var prefixPattern = prefix + "%";
        var wordPattern = "% " + prefix + "%";
        var keyPattern = key + "%";
        return venues.Where(v =>
            EF.Functions.Like(v.SortKey, prefixPattern)
            || EF.Functions.Like(v.SortKey, wordPattern)
            || EF.Functions.Like(v.NameKey, keyPattern)
            || EF.Functions.TrigramsSimilarity(v.NameKey, key) >= ListPeopleHandler.SearchSimilarity);
    }
}
