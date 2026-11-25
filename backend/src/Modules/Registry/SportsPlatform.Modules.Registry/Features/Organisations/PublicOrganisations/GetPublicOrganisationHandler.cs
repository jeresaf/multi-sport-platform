using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.Media;
using SportsPlatform.Modules.Registry.Features.Teams;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Organisations.PublicOrganisations;

/// <summary>
/// One organisation of the request's public tenant (query filter and RLS) by ID or slug, in any status but
/// <c>ARCHIVED</c> (S07 D17). The primary venue and the parent are embedded only while they aren't archived (an archived
/// one 404s on its own read); children and teams leave archived ones out, as the admin read does.
/// </summary>
public sealed class GetPublicOrganisationHandler(RegistryDbContext context, PublicImages images)
{
    public async Task<Result<PublicOrganisationResponse>> HandleAsync(string idOrSlug, CancellationToken cancellationToken)
    {
        var key = IdOrSlug.Parse(idOrSlug);
        if (!key.CanMatch)
        {
            return RegistryErrors.OrganisationNotFound;
        }

        var organisations = context.Set<Organisation>().AsNoTracking();
        var o = await (key.Id is { } id ? organisations.Where(o => o.Id == id) : organisations.Where(o => o.Slug == key.Slug))
            .Where(o => o.Status != OrganisationStatus.Archived)
            .SingleOrDefaultAsync(cancellationToken);
        if (o is null)
        {
            return RegistryErrors.OrganisationNotFound;
        }

        var venue = o.PrimaryVenueId is { } venueId
            ? await context.Set<Venue>().AsNoTracking()
                .Where(v => v.Id == venueId && v.Status != VenueStatus.Archived)
                .Select(v => new OrganisationVenueResponse(v.Id, v.Name, v.Slug))
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var links = context.Set<OrganisationParentLink>().AsNoTracking().Where(l => l.ValidTo == null);
        var parent = await links
            .Where(l => l.OrganisationId == o.Id)
            .Join(organisations, l => l.ParentId, p => p.Id, (_, p) => p)
            .Where(p => p.Status != OrganisationStatus.Archived)
            .Select(p => new OrganisationSummaryResponse(p.Id, p.DisplayName, p.Slug, p.Status))
            .SingleOrDefaultAsync(cancellationToken);
        var children = await links
            .Where(l => l.ParentId == o.Id)
            .Join(organisations, l => l.OrganisationId, c => c.Id, (_, c) => c)
            .Where(c => c.Status != OrganisationStatus.Archived)
            .OrderBy(c => c.SortKey).ThenBy(c => c.Id)
            .Select(c => new OrganisationSummaryResponse(c.Id, c.DisplayName, c.Slug, c.Status))
            .ToListAsync(cancellationToken);

        var teams = await TeamOrder.Ordered(TeamOrder.Ranked(context.Set<Team>().AsNoTracking()
                .Where(t => t.OrganisationId == o.Id && t.Status != TeamStatus.Archived)))
            .Select(t => new OrganisationTeamResponse(
                t.Team.Id, t.Team.Name, t.Team.Slug, t.Team.Variant, t.Team.GenderCategory, t.Team.AgeGroup, t.Team.Level,
                t.Team.Status))
            .ToListAsync(cancellationToken);

        return Result.Success(new PublicOrganisationResponse(
            o.Id, o.Type, o.DisplayName, o.ShortName, o.Slug, o.Country, o.City, o.FoundedYear, o.Website, o.Description,
            venue, parent, children, teams, o.Status, images.Of(o.LogoAssetId)));
    }
}
