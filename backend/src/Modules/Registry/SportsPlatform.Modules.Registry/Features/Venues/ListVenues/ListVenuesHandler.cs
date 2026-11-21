using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.People.ListPeople;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Venues.ListVenues;

/// <summary>A venue in the directory: enough to recognise it and open it (S07 D19; every field is public, D16).</summary>
public sealed record VenueListItem(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string Name,
    [property: PersonalData(PersonalDataClass.Public)] string? ShortName,
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] VenueType Type,
    [property: PersonalData(PersonalDataClass.Public)] string City,
    [property: PersonalData(PersonalDataClass.Public)] string Country,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<string> SupportedVariants,
    [property: PersonalData(PersonalDataClass.Public)] VenueStatus Status);

public sealed record ListVenuesResponse(
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<VenueListItem> Items,
    [property: PersonalData(PersonalDataClass.Public)] PageInfo Page);

/// <summary>
/// The venue directory of the request's tenant (query filter and RLS), sorted by sort key (the normalised name, word
/// order kept, collation "C") and ID (S07 D19). <c>q</c> matches the name by prefix (of the name or of one of its
/// words) or trigram similarity; aliases and external identifiers join it in S07-T1c. <c>variant</c> keeps venues
/// that support it, <c>city</c> venues in that city (normalised, as the duplicate warning's "same city"). Without a
/// status, archived venues are left out.
/// </summary>
public sealed class ListVenuesHandler(RegistryDbContext context)
{
    public async Task<ListVenuesResponse> HandleAsync(VenuesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var venues = context.Set<Venue>().AsNoTracking();
        venues = query.Status is { } status
            ? venues.Where(v => v.Status == status)
            : venues.Where(v => v.Status != VenueStatus.Archived);

        if (query.Variant is { } variant)
        {
            venues = venues.Where(v => v.SupportedVariants.Contains(variant));
        }

        if (query.City is { } city)
        {
            // A city without a letter or a digit has no key and matches no venue (as the duplicate warning's).
            var cityKey = Venue.CityKeyOf(city);
            venues = cityKey.Length == 0 ? venues.Where(_ => false) : venues.Where(v => v.CityKey == cityKey);
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
            .Select(v => new Row(
                v.Id, v.Name, v.ShortName, v.Slug, v.Type, v.City, v.Country, v.SupportedVariants, v.Status, v.SortKey))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > query.Limit;
        var page = rows.Take(query.Limit).ToList();
        var items = page.Select(row => new VenueListItem(
            row.Id, row.Name, row.ShortName, row.Slug, row.Type, row.City, row.Country, row.SupportedVariants, row.Status)).ToList();
        var next = hasMore ? new VenueCursor(page[^1].SortKey, page[^1].Id).Encode() : null;
        return new ListVenuesResponse(items, new PageInfo(next, hasMore));
    }

    private static IQueryable<Venue> Matching(IQueryable<Venue> venues, string search)
    {
        // The normalised text as typed ("kiwatule mea") for prefixes; its sorted tokens to compare with name keys. Text
        // without a letter or a digit matches no name.
        var prefix = Venue.SortKeyOf(search);
        var key = Venue.NameKeyOf(search);
        if (prefix.Length == 0)
        {
            return venues.Where(_ => false);
        }

        // Keys hold letters, digits and spaces only, so no pattern has a wildcard of its own.
        var prefixPattern = prefix + "%";
        var wordPattern = "% " + prefix + "%";
        var keyPattern = key + "%";
        return venues.Where(v =>
            EF.Functions.Like(v.SortKey, prefixPattern)
            || EF.Functions.Like(v.SortKey, wordPattern)
            || EF.Functions.Like(v.NameKey, keyPattern)
            || EF.Functions.TrigramsSimilarity(v.NameKey, key) >= ListPeopleHandler.SearchSimilarity);
    }

    private sealed record Row(
        Guid Id, string Name, string? ShortName, string Slug, VenueType Type, string City, string Country,
        IReadOnlyList<string> SupportedVariants, VenueStatus Status, string SortKey);
}
