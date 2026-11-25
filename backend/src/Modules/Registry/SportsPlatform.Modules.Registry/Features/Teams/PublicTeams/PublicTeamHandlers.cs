using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.Media;
using SportsPlatform.Modules.Registry.Features.People.ListPeople;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Teams.PublicTeams;

/// <summary>
/// One team of the request's public tenant (query filter and RLS) by ID or slug, in any status but <c>ARCHIVED</c> (S07
/// D17), with its former names (D16). The organisation and the home venue are embedded only while they aren't archived
/// (an archived one 404s on its own read). Nicknames, spellings and identifiers are <c>INTERNAL</c> and never read.
/// </summary>
public sealed class GetPublicTeamHandler(RegistryDbContext context, PublicImages images)
{
    public async Task<Result<PublicTeamResponse>> HandleAsync(string idOrSlug, CancellationToken cancellationToken)
    {
        var key = IdOrSlug.Parse(idOrSlug);
        if (!key.CanMatch)
        {
            return RegistryErrors.TeamNotFound;
        }

        var teams = context.Set<Team>().AsNoTracking();
        var t = await (key.Id is { } id ? teams.Where(t => t.Id == id) : teams.Where(t => t.Slug == key.Slug))
            .Where(t => t.Status != TeamStatus.Archived)
            .SingleOrDefaultAsync(cancellationToken);
        if (t is null)
        {
            return RegistryErrors.TeamNotFound;
        }

        var organisation = t.OrganisationId is { } organisationId
            ? await context.Set<Organisation>().AsNoTracking()
                .Where(o => o.Id == organisationId && o.Status != OrganisationStatus.Archived)
                .Select(o => new TeamOrganisationResponse(o.Id, o.DisplayName, o.Slug))
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var venue = t.HomeVenueId is { } venueId
            ? await context.Set<Venue>().AsNoTracking()
                .Where(v => v.Id == venueId && v.Status != VenueStatus.Archived)
                .Select(v => new TeamVenueResponse(v.Id, v.Name, v.Slug))
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        // Former names only (never removed, D10): newest first, as the admin read.
        var formerNames = await context.Set<TeamAlias>().AsNoTracking()
            .Where(a => a.TeamId == t.Id && a.Kind == AliasKind.FormerName && a.RemovedAt == null)
            .OrderByDescending(a => a.ValidFrom).ThenBy(a => a.Id)
            .Select(a => new TeamFormerNameResponse(a.Id, a.Name, a.ShortName, a.Code, a.ValidFrom!.Value, a.ValidTo!.Value))
            .ToListAsync(cancellationToken);

        return Result.Success(new PublicTeamResponse(
            t.Id, t.Variant, t.GenderCategory, t.AgeGroup, t.Level, t.Name, t.ShortName, t.NameSince, t.Slug, t.Code, t.Motto,
            t.PrimaryColour, t.SecondaryColour, t.TertiaryColour, t.FoundedYear, organisation, venue, t.Status,
            images.Of(t.LogoAssetId), images.Of(t.BadgeAssetId), formerNames));
    }
}

/// <summary>
/// The public team directory of the request's public tenant (S07 D17), in the admin directory's order
/// (<see cref="TeamOrder"/>) and with its cursor. Only one status at a time, <c>ACTIVE</c> by default, never
/// <c>ARCHIVED</c>. <c>q</c> matches only <c>PUBLIC</c> text (D16): the name and short name as the admin directory does,
/// and a former name (owner, plan 2026-11-25); never a nickname, a spelling or an identifier, so the public list can't be
/// used to probe them. An item's organisation is null when it is archived, as on the read.
/// </summary>
public sealed class ListPublicTeamsHandler(RegistryDbContext context, PublicImages images)
{
    public async Task<ListPublicTeamsResponse> HandleAsync(PublicTeamsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var filters = query.Filters;

        var teams = context.Set<Team>().AsNoTracking().Where(t => t.Status == query.Status);
        if (filters.Variant is { } variant)
        {
            teams = teams.Where(t => t.Variant == variant);
        }

        if (filters.OrganisationId is { } organisationId)
        {
            teams = teams.Where(t => t.OrganisationId == organisationId);
        }

        if (filters.GenderCategory is { } gender)
        {
            teams = teams.Where(t => t.GenderCategory == gender);
        }

        if (filters.AgeGroup is { } ageGroup)
        {
            teams = teams.Where(t => t.AgeGroup == ageGroup);
        }

        if (filters.Level is { } level)
        {
            teams = teams.Where(t => t.Level == level);
        }

        if (filters.Search is { } search)
        {
            teams = Matching(teams, search);
        }

        var ranked = TeamOrder.Ranked(teams);
        if (filters.After is { } after)
        {
            ranked = ranked.Where(t =>
                t.Team.SortKey.CompareTo(after.SortKey) > 0
                || (t.Team.SortKey == after.SortKey
                    && (t.CategoryRank > after.CategoryRank
                        || (t.CategoryRank == after.CategoryRank && t.Team.Id.CompareTo(after.Id) > 0))));
        }

        var rows = await TeamOrder.Ordered(ranked)
            .Take(filters.Limit + 1)
            .Select(t => new Row(
                t.Team.Id, t.Team.Name, t.Team.ShortName, t.Team.Slug, t.Team.Variant, t.Team.GenderCategory, t.Team.AgeGroup,
                t.Team.Level, t.Team.OrganisationId, t.Team.Status, t.Team.LogoAssetId, t.Team.BadgeAssetId, t.Team.SortKey,
                t.CategoryRank))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > filters.Limit;
        var page = rows.Take(filters.Limit).ToList();

        // The page's organisations in one query; archived ones aren't linked (D17).
        var organisationIds = page.Where(row => row.OrganisationId is not null).Select(row => row.OrganisationId!.Value).Distinct().ToList();
        var organisations = organisationIds.Count == 0
            ? []
            : await context.Set<Organisation>().AsNoTracking()
                .Where(o => organisationIds.Contains(o.Id) && o.Status != OrganisationStatus.Archived)
                .Select(o => new TeamOrganisationResponse(o.Id, o.DisplayName, o.Slug))
                .ToDictionaryAsync(o => o.Id, cancellationToken);

        var items = page.Select(row => new PublicTeamListItem(
            row.Id, row.Name, row.ShortName, row.Slug, row.Variant, row.GenderCategory, row.AgeGroup, row.Level,
            row.OrganisationId is { } id ? organisations.GetValueOrDefault(id) : null,
            row.Status, images.Of(row.LogoAssetId), images.Of(row.BadgeAssetId))).ToList();
        var next = hasMore ? new TeamDirectoryCursor(page[^1].SortKey, page[^1].CategoryRank, page[^1].Id).Encode() : null;
        return new ListPublicTeamsResponse(items, new PublicPageInfo(next, hasMore));
    }

    private static IQueryable<Team> Matching(IQueryable<Team> teams, string search)
    {
        // As the admin directory's, without the INTERNAL parts (nicknames, spellings, identifiers). Text without a letter
        // or a digit has no key and matches nothing.
        var prefix = Team.SortKeyOf(search);
        if (prefix.Length == 0)
        {
            return teams.Where(_ => false);
        }

        var key = Team.NameKeyOf(search);
        var prefixPattern = prefix + "%";
        var wordPattern = "% " + prefix + "%";
        var keyPattern = key + "%";
        return teams.Where(t =>
            EF.Functions.Like(t.SortKey, prefixPattern)
            || EF.Functions.Like(t.SortKey, wordPattern)
            || EF.Functions.Like(t.NameKey, keyPattern)
            || EF.Functions.TrigramsSimilarity(t.NameKey, key) >= ListPeopleHandler.SearchSimilarity
            || (t.ShortNameKey != null && EF.Functions.Like(t.ShortNameKey, keyPattern))
            || t.Aliases.Any(a => a.Kind == AliasKind.FormerName && a.RemovedAt == null
                && (EF.Functions.Like(a.NameKey, keyPattern)
                    || EF.Functions.Like(a.NameKey, prefixPattern)
                    || EF.Functions.Like(a.NameKey, wordPattern)
                    || EF.Functions.TrigramsSimilarity(a.NameKey, key) >= ListPeopleHandler.SearchSimilarity)));
    }

    private sealed record Row(
        Guid Id, string Name, string? ShortName, string Slug, string Variant, GenderCategory GenderCategory, AgeGroup AgeGroup,
        TeamLevel Level, Guid? OrganisationId, TeamStatus Status, Guid? LogoAssetId, Guid? BadgeAssetId, string SortKey,
        int CategoryRank);
}
