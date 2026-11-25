using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.Media;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Teams.GetTeam;

/// <summary>
/// One team of the request's tenant, whatever its status, with its organisation and home venue as summaries, its former
/// names, current aliases and external identifiers (ended ones included); another tenant's team is not found (RLS).
/// </summary>
public sealed class GetTeamHandler(RegistryDbContext context, RegistryImageUrls images)
{
    public async Task<Result<TeamResponse>> HandleAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var found = await context.Set<Team>().AsNoTracking()
            .Where(t => t.Id == teamId)
            .Select(t => new { Team = t, Version = EF.Property<uint>(t, AuditColumns.Version) })
            .SingleOrDefaultAsync(cancellationToken);
        if (found is null)
        {
            return RegistryErrors.TeamNotFound;
        }

        var t = found.Team;
        var organisation = t.OrganisationId is { } organisationId
            ? await context.Set<Organisation>().AsNoTracking()
                .Where(o => o.Id == organisationId)
                .Select(o => new TeamOrganisationResponse(o.Id, o.DisplayName, o.Slug))
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var venue = t.HomeVenueId is { } venueId
            ? await context.Set<Venue>().AsNoTracking()
                .Where(v => v.Id == venueId)
                .Select(v => new TeamVenueResponse(v.Id, v.Name, v.Slug))
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        // Former names are never removed; a nickname or spelling shows while it is current (S07 D10).
        var aliases = await context.Set<TeamAlias>().AsNoTracking()
            .Where(a => a.TeamId == teamId && a.RemovedAt == null)
            .ToListAsync(cancellationToken);
        var formerNames = aliases
            .Where(a => a.IsFormerName)
            .OrderByDescending(a => a.ValidFrom)
            .Select(a => new TeamFormerNameResponse(a.Id, a.Name, a.ShortName, a.Code, a.ValidFrom!.Value, a.ValidTo!.Value))
            .ToList();
        var others = aliases
            .Where(a => !a.IsFormerName)
            .OrderBy(a => a.Id)
            .Select(a => new TeamAliasResponse(a.Id, a.Name, a.Kind))
            .ToList();

        var identifiers = await context.Set<ExternalIdentifier>().AsNoTracking()
            .Where(i => i.EntityType == ExternalIdentifierEntityType.Team && i.EntityId == teamId)
            .OrderBy(i => i.Authority).ThenBy(i => i.Id)
            .Select(i => new ExternalIdentifierResponse(i.Id, i.Authority, i.Value, i.ValidFrom, i.ValidTo))
            .ToListAsync(cancellationToken);

        var logo = await images.SignAsync(TeamsEndpoints.EntityType, t.Id, t.LogoAssetId, cancellationToken);
        var badge = await images.SignAsync(TeamsEndpoints.EntityType, t.Id, t.BadgeAssetId, cancellationToken);

        return Result.Success(new TeamResponse(
            t.Id, t.Variant, t.GenderCategory, t.AgeGroup, t.Level, t.Name, t.ShortName, t.NameSince, t.Slug, t.Code, t.Motto,
            t.PrimaryColour, t.SecondaryColour, t.TertiaryColour, t.FoundedYear, organisation, venue, t.Status, logo, badge,
            formerNames, others, identifiers, found.Version));
    }
}
