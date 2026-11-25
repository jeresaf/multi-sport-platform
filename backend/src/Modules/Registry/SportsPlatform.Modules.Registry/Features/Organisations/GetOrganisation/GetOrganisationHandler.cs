using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.Media;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Features.Teams;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Organisations.GetOrganisation;

/// <summary>
/// One organisation of the request's tenant, whatever its status, with its primary venue, its current parent and its
/// current children (archived ones left out) as summaries; another tenant's organisation is not found (RLS). The parent
/// and the children come from the open links (S07 D5; the index on the open links' parent serves the children). Its
/// current aliases and its external identifiers, ended ones included, come with it (S07 D10); so do its teams, archived ones
/// left out, in <see cref="TeamOrder"/> (S07-T3b-2; the index on the teams' organisation serves them), and its logo's
/// signed URLs (S07 D15).
/// </summary>
public sealed class GetOrganisationHandler(RegistryDbContext context, RegistryImageUrls images)
{
    public async Task<Result<OrganisationResponse>> HandleAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        var found = await context.Set<Organisation>().AsNoTracking()
            .Where(o => o.Id == organisationId)
            .Select(o => new { Organisation = o, Version = EF.Property<uint>(o, AuditColumns.Version) })
            .SingleOrDefaultAsync(cancellationToken);
        if (found is null)
        {
            return RegistryErrors.OrganisationNotFound;
        }

        var o = found.Organisation;
        var venue = o.PrimaryVenueId is { } venueId
            ? await context.Set<Venue>().AsNoTracking()
                .Where(v => v.Id == venueId)
                .Select(v => new OrganisationVenueResponse(v.Id, v.Name, v.Slug))
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var links = context.Set<OrganisationParentLink>().AsNoTracking().Where(l => l.ValidTo == null);
        var organisations = context.Set<Organisation>().AsNoTracking();
        var parent = await links
            .Where(l => l.OrganisationId == organisationId)
            .Join(organisations, l => l.ParentId, p => p.Id, (_, p) => new OrganisationSummaryResponse(p.Id, p.DisplayName, p.Slug, p.Status))
            .SingleOrDefaultAsync(cancellationToken);
        var children = await links
            .Where(l => l.ParentId == organisationId)
            .Join(organisations, l => l.OrganisationId, c => c.Id, (_, c) => c)
            .Where(c => c.Status != OrganisationStatus.Archived)
            .OrderBy(c => c.SortKey).ThenBy(c => c.Id)
            .Select(c => new OrganisationSummaryResponse(c.Id, c.DisplayName, c.Slug, c.Status))
            .ToListAsync(cancellationToken);

        var teams = await TeamOrder.Ordered(TeamOrder.Ranked(context.Set<Team>().AsNoTracking()
                .Where(t => t.OrganisationId == organisationId && t.Status != TeamStatus.Archived)))
            .Select(t => new OrganisationTeamResponse(
                t.Team.Id, t.Team.Name, t.Team.Slug, t.Team.Variant, t.Team.GenderCategory, t.Team.AgeGroup, t.Team.Level,
                t.Team.Status))
            .ToListAsync(cancellationToken);

        var aliases = await context.Set<OrganisationAlias>().AsNoTracking()
            .Where(a => a.OrganisationId == organisationId && a.RemovedAt == null)
            .OrderBy(a => a.Id)
            .Select(a => new OrganisationAliasResponse(a.Id, a.Name, a.Kind))
            .ToListAsync(cancellationToken);

        var identifiers = await context.Set<ExternalIdentifier>().AsNoTracking()
            .Where(i => i.EntityType == ExternalIdentifierEntityType.Organisation && i.EntityId == organisationId)
            .OrderBy(i => i.Authority).ThenBy(i => i.Id)
            .Select(i => new ExternalIdentifierResponse(i.Id, i.Authority, i.Value, i.ValidFrom, i.ValidTo))
            .ToListAsync(cancellationToken);

        var logo = await images.SignAsync(OrganisationsEndpoints.EntityType, o.Id, o.LogoAssetId, cancellationToken);

        return Result.Success(new OrganisationResponse(
            o.Id, o.Type, o.DisplayName, o.LegalName, o.ShortName, o.Slug, o.Country, o.City, o.FoundedYear, o.Website, o.Email,
            o.Phone, o.Description, venue, parent, children, teams, o.Status, logo, aliases, identifiers, found.Version));
    }
}
