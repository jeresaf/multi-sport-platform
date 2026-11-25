using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.Media;
using SportsPlatform.Modules.Registry.Features.People.ListPeople;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Organisations.PublicOrganisations;

/// <summary>
/// The public organisation directory of the request's public tenant (S07 D17), in the admin directory's order (sort key,
/// ID) and with its cursor. Only one status at a time, <c>ACTIVE</c> by default, never <c>ARCHIVED</c>. <c>q</c> matches
/// only <c>PUBLIC</c> text (D16): the display name as the admin directory does, and the short name by name-key prefix;
/// never the legal name, an alias or an identifier, so the public list can't be used to probe them.
/// </summary>
public sealed class ListPublicOrganisationsHandler(RegistryDbContext context, PublicImages images)
{
    public async Task<ListPublicOrganisationsResponse> HandleAsync(PublicOrganisationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var organisations = context.Set<Organisation>().AsNoTracking().Where(o => o.Status == query.Status);
        if (query.Type is { } type)
        {
            organisations = organisations.Where(o => o.Type == type);
        }

        if (query.Search is { } search)
        {
            organisations = Matching(organisations, search);
        }

        if (query.After is { } after)
        {
            organisations = organisations.Where(o =>
                o.SortKey.CompareTo(after.SortKey) > 0 || (o.SortKey == after.SortKey && o.Id.CompareTo(after.Id) > 0));
        }

        var rows = await organisations
            .OrderBy(o => o.SortKey).ThenBy(o => o.Id)
            .Take(query.Limit + 1)
            .Select(o => new Row(
                o.Id, o.DisplayName, o.ShortName, o.Slug, o.Type, o.City, o.Country, o.Status, o.LogoAssetId, o.SortKey))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > query.Limit;
        var page = rows.Take(query.Limit).ToList();
        var items = page.Select(row => new PublicOrganisationListItem(
            row.Id, row.DisplayName, row.ShortName, row.Slug, row.Type, row.City, row.Country, row.Status,
            images.Of(row.LogoAssetId))).ToList();
        var next = hasMore ? new DirectoryCursor(page[^1].SortKey, page[^1].Id).Encode() : null;
        return new ListPublicOrganisationsResponse(items, new PublicPageInfo(next, hasMore));
    }

    private static IQueryable<Organisation> Matching(IQueryable<Organisation> organisations, string search)
    {
        // As the admin directory's, without the INTERNAL parts. Text without a letter or a digit has no key and matches
        // nothing (the admin directory's identifiers aren't public).
        var prefix = Organisation.SortKeyOf(search);
        if (prefix.Length == 0)
        {
            return organisations.Where(_ => false);
        }

        var key = Organisation.NameKeyOf(search);
        var prefixPattern = prefix + "%";
        var wordPattern = "% " + prefix + "%";
        var keyPattern = key + "%";
        return organisations.Where(o =>
            EF.Functions.Like(o.SortKey, prefixPattern)
            || EF.Functions.Like(o.SortKey, wordPattern)
            || EF.Functions.Like(o.DisplayNameKey, keyPattern)
            || EF.Functions.TrigramsSimilarity(o.DisplayNameKey, key) >= ListPeopleHandler.SearchSimilarity
            || (o.ShortNameKey != null && EF.Functions.Like(o.ShortNameKey, keyPattern)));
    }

    private sealed record Row(
        Guid Id, string DisplayName, string? ShortName, string Slug, OrganisationType Type, string? City, string Country,
        OrganisationStatus Status, Guid? LogoAssetId, string SortKey);
}
