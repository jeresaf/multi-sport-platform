using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.People.ListPeople;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Organisations.ListOrganisations;

/// <summary>
/// An organisation in the directory: enough to recognise it and open it (S07 D19; every field is public, D16 — the email
/// and phone, which are internal, are left out).
/// </summary>
public sealed record OrganisationListItem(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
    [property: PersonalData(PersonalDataClass.Public)] string? ShortName,
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationType Type,
    [property: PersonalData(PersonalDataClass.Public)] string? City,
    [property: PersonalData(PersonalDataClass.Public)] string Country,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationStatus Status);

// PUBLIC, as venues' list: every item field is one the public reads may show (D16), and the cursor carries only a sort
// key built from the public display name. A plain comment: a summary would reach OpenAPI.
public sealed record ListOrganisationsResponse(
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<OrganisationListItem> Items,
    [property: PersonalData(PersonalDataClass.Public)] PageInfo Page);

/// <summary>
/// The organisation directory of the request's tenant (query filter and RLS), sorted by sort key (the normalised display
/// name, word order kept, collation "C") and ID (S07 D19). <c>q</c> matches the display name by prefix (of the name or of
/// one of its words), name-key prefix or trigram similarity, and the legal or short name by name-key prefix (owner, plan
/// 2026-11-23); aliases and identifiers join in S07-T2d. Without a status, archived organisations are left out.
/// </summary>
public sealed class ListOrganisationsHandler(RegistryDbContext context)
{
    public async Task<ListOrganisationsResponse> HandleAsync(OrganisationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var organisations = context.Set<Organisation>().AsNoTracking();
        organisations = query.Status is { } status
            ? organisations.Where(o => o.Status == status)
            : organisations.Where(o => o.Status != OrganisationStatus.Archived);

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
            .Select(o => new Row(o.Id, o.DisplayName, o.ShortName, o.Slug, o.Type, o.City, o.Country, o.Status, o.SortKey))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > query.Limit;
        var page = rows.Take(query.Limit).ToList();
        var items = page.Select(row => new OrganisationListItem(
            row.Id, row.DisplayName, row.ShortName, row.Slug, row.Type, row.City, row.Country, row.Status)).ToList();
        var next = hasMore ? new DirectoryCursor(page[^1].SortKey, page[^1].Id).Encode() : null;
        return new ListOrganisationsResponse(items, new PageInfo(next, hasMore));
    }

    private static IQueryable<Organisation> Matching(IQueryable<Organisation> organisations, string search)
    {
        // The normalised text as typed ("ntinda swi") for prefixes; its sorted tokens to compare with name keys. Text
        // without a letter or a digit has no key and matches nothing (identifiers join in S07-T2d).
        var prefix = Organisation.SortKeyOf(search);
        if (prefix.Length == 0)
        {
            return organisations.Where(_ => false);
        }

        // Keys hold letters, digits and spaces only, so no pattern has a wildcard of its own.
        var key = Organisation.NameKeyOf(search);
        var prefixPattern = prefix + "%";
        var wordPattern = "% " + prefix + "%";
        var keyPattern = key + "%";
        return organisations.Where(o =>
            EF.Functions.Like(o.SortKey, prefixPattern)
            || EF.Functions.Like(o.SortKey, wordPattern)
            || EF.Functions.Like(o.DisplayNameKey, keyPattern)
            || EF.Functions.TrigramsSimilarity(o.DisplayNameKey, key) >= ListPeopleHandler.SearchSimilarity
            || (o.LegalNameKey != null && EF.Functions.Like(o.LegalNameKey, keyPattern))
            || (o.ShortNameKey != null && EF.Functions.Like(o.ShortNameKey, keyPattern)));
    }

    private sealed record Row(
        Guid Id, string DisplayName, string? ShortName, string Slug, OrganisationType Type, string? City, string Country,
        OrganisationStatus Status, string SortKey);
}
