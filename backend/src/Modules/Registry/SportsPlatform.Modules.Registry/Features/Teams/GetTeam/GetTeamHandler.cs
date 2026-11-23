using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Teams.GetTeam;

/// <summary>
/// One team of the request's tenant, whatever its status, with its organisation and home venue as summaries; another
/// tenant's team is not found (RLS).
/// </summary>
public sealed class GetTeamHandler(RegistryDbContext context)
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

        return Result.Success(new TeamResponse(
            t.Id, t.Variant, t.GenderCategory, t.AgeGroup, t.Level, t.Name, t.ShortName, t.NameSince, t.Slug, t.Code, t.Motto,
            t.PrimaryColour, t.SecondaryColour, t.TertiaryColour, t.FoundedYear, organisation, venue, t.Status, found.Version));
    }
}
