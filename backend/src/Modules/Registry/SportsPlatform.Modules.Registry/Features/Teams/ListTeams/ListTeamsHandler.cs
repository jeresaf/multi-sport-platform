using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.People.ListPeople;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Teams.ListTeams;

/// <summary>
/// A team in the directory: enough to recognise it and open it (S07 D19; every field is public, D16). The organisation
/// is the team's, whatever its status now, or null for an independent team.
/// </summary>
public sealed record TeamListItem(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string Name,
    [property: PersonalData(PersonalDataClass.Public)] string? ShortName,
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] string Variant,
    [property: PersonalData(PersonalDataClass.Public)] GenderCategory GenderCategory,
    [property: PersonalData(PersonalDataClass.Public)] AgeGroup AgeGroup,
    [property: PersonalData(PersonalDataClass.Public)] TeamLevel Level,
    [property: PersonalData(PersonalDataClass.Public)] TeamOrganisationResponse? Organisation,
    [property: PersonalData(PersonalDataClass.Public)] TeamStatus Status);

// PUBLIC, as the other directories: every item field is one the public reads may show (D16), and the cursor carries
// only a sort key built from the public name, a category rank and an ID. A plain comment: a summary would reach OpenAPI.
public sealed record ListTeamsResponse(
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<TeamListItem> Items,
    [property: PersonalData(PersonalDataClass.Public)] PageInfo Page);

/// <summary>
/// The team directory of the request's tenant (query filter and RLS), in <see cref="TeamOrder"/>: name (the sort key,
/// collation "C"), category, ID (S07 D19; owner, plan 2026-11-24). <c>q</c> matches the name by prefix (of the name or
/// of one of its words), name-key prefix or trigram similarity, the short name by name-key prefix, and a current alias
/// (former names included, S07-T3c) as organisations' aliases, and an external identifier's value exactly, ended ones
/// included (S07-T3d). <c>homeVenueId</c> keeps the teams whose home venue it is (the venue's Teams tab,
/// S07-T6a). Without a status, archived teams are left out. An unknown variant or organisation keeps none.
/// </summary>
public sealed class ListTeamsHandler(RegistryDbContext context)
{
    public async Task<ListTeamsResponse> HandleAsync(TeamsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var teams = context.Set<Team>().AsNoTracking();
        teams = query.Status is { } status
            ? teams.Where(t => t.Status == status)
            : teams.Where(t => t.Status != TeamStatus.Archived);

        if (query.Variant is { } variant)
        {
            teams = teams.Where(t => t.Variant == variant);
        }

        if (query.OrganisationId is { } organisationId)
        {
            teams = teams.Where(t => t.OrganisationId == organisationId);
        }

        if (query.HomeVenueId is { } homeVenueId)
        {
            teams = teams.Where(t => t.HomeVenueId == homeVenueId);
        }

        if (query.GenderCategory is { } gender)
        {
            teams = teams.Where(t => t.GenderCategory == gender);
        }

        if (query.AgeGroup is { } ageGroup)
        {
            teams = teams.Where(t => t.AgeGroup == ageGroup);
        }

        if (query.Level is { } level)
        {
            teams = teams.Where(t => t.Level == level);
        }

        if (query.Search is { } search)
        {
            teams = Matching(teams, search);
        }

        var ranked = TeamOrder.Ranked(teams);
        if (query.After is { } after)
        {
            ranked = ranked.Where(t =>
                t.Team.SortKey.CompareTo(after.SortKey) > 0
                || (t.Team.SortKey == after.SortKey
                    && (t.CategoryRank > after.CategoryRank
                        || (t.CategoryRank == after.CategoryRank && t.Team.Id.CompareTo(after.Id) > 0))));
        }

        var rows = await TeamOrder.Ordered(ranked)
            .Take(query.Limit + 1)
            .Select(t => new Row(
                t.Team.Id, t.Team.Name, t.Team.ShortName, t.Team.Slug, t.Team.Variant, t.Team.GenderCategory, t.Team.AgeGroup,
                t.Team.Level, t.Team.OrganisationId, t.Team.Status, t.Team.SortKey, t.CategoryRank))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > query.Limit;
        var page = rows.Take(query.Limit).ToList();

        // The page's organisations in one query, whatever their status now.
        var organisationIds = page.Where(row => row.OrganisationId is not null).Select(row => row.OrganisationId!.Value).Distinct().ToList();
        var organisations = organisationIds.Count == 0
            ? []
            : await context.Set<Organisation>().AsNoTracking()
                .Where(o => organisationIds.Contains(o.Id))
                .Select(o => new TeamOrganisationResponse(o.Id, o.DisplayName, o.Slug))
                .ToDictionaryAsync(o => o.Id, cancellationToken);

        var items = page.Select(row => new TeamListItem(
            row.Id, row.Name, row.ShortName, row.Slug, row.Variant, row.GenderCategory, row.AgeGroup, row.Level,
            row.OrganisationId is { } id ? organisations.GetValueOrDefault(id) : null,
            row.Status)).ToList();
        var next = hasMore ? new TeamDirectoryCursor(page[^1].SortKey, page[^1].CategoryRank, page[^1].Id).Encode() : null;
        return new ListTeamsResponse(items, new PageInfo(next, hasMore));
    }

    private IQueryable<Team> Matching(IQueryable<Team> teams, string search)
    {
        // The normalised text as typed ("ntinda swi") for prefixes; its sorted tokens to compare with name keys. Text
        // without a letter or a digit has no key and can only be an identifier's value.
        var prefix = Team.SortKeyOf(search);
        var key = Team.NameKeyOf(search);
        var identifiers = context.Set<ExternalIdentifier>()
            .Where(i => i.EntityType == ExternalIdentifierEntityType.Team && i.Value == search);
        if (prefix.Length == 0)
        {
            return teams.Where(t => identifiers.Any(i => i.EntityId == t.Id));
        }

        // Keys hold letters, digits and spaces only, so no pattern has a wildcard of its own. An alias has a name key
        // only (sorted tokens): a prefix of the key or of one of its words finds it. Nicknames, spellings and identifiers
        // are INTERNAL (D16): matching them is for this admin directory only; a public list (T5) must not reuse this to
        // probe them.
        var prefixPattern = prefix + "%";
        var wordPattern = "% " + prefix + "%";
        var keyPattern = key + "%";
        return teams.Where(t =>
            EF.Functions.Like(t.SortKey, prefixPattern)
            || EF.Functions.Like(t.SortKey, wordPattern)
            || EF.Functions.Like(t.NameKey, keyPattern)
            || EF.Functions.TrigramsSimilarity(t.NameKey, key) >= ListPeopleHandler.SearchSimilarity
            || (t.ShortNameKey != null && EF.Functions.Like(t.ShortNameKey, keyPattern))
            || t.Aliases.Any(a => a.RemovedAt == null
                && (EF.Functions.Like(a.NameKey, keyPattern)
                    || EF.Functions.Like(a.NameKey, prefixPattern)
                    || EF.Functions.Like(a.NameKey, wordPattern)
                    || EF.Functions.TrigramsSimilarity(a.NameKey, key) >= ListPeopleHandler.SearchSimilarity))
            || identifiers.Any(i => i.EntityId == t.Id));
    }

    private sealed record Row(
        Guid Id, string Name, string? ShortName, string Slug, string Variant, GenderCategory GenderCategory, AgeGroup AgeGroup,
        TeamLevel Level, Guid? OrganisationId, TeamStatus Status, string SortKey, int CategoryRank);
}
