using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Organisations.GetOrganisation;

/// <summary>
/// One organisation of the request's tenant, whatever its status, with its primary venue as a summary; another tenant's
/// organisation is not found (RLS).
/// </summary>
public sealed class GetOrganisationHandler(RegistryDbContext context)
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

        return Result.Success(new OrganisationResponse(
            o.Id, o.Type, o.DisplayName, o.LegalName, o.ShortName, o.Slug, o.Country, o.City, o.FoundedYear, o.Website, o.Email,
            o.Phone, o.Description, venue, o.Status, found.Version));
    }
}
