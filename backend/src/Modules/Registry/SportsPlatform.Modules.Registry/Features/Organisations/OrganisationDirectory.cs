using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Registry.Contracts.Organisations;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Infrastructure;
using DomainStatus = SportsPlatform.Modules.Registry.Domain.Organisations.OrganisationStatus;
using DomainType = SportsPlatform.Modules.Registry.Domain.Organisations.OrganisationType;
using OrganisationStatus = SportsPlatform.Modules.Registry.Contracts.Organisations.OrganisationStatus;
using OrganisationType = SportsPlatform.Modules.Registry.Contracts.Organisations.OrganisationType;

namespace SportsPlatform.Modules.Registry.Features.Organisations;

/// <summary><see cref="IOrganisationDirectory"/> over the scope's tenant (query filter and RLS).</summary>
public sealed class OrganisationDirectory(RegistryDbContext context) : IOrganisationDirectory
{
    public async Task<IReadOnlyList<OrganisationSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> organisationIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organisationIds);
        if (organisationIds.Count == 0)
        {
            return [];
        }

        var ids = organisationIds.Distinct().ToArray();
        var rows = await context.Set<Organisation>().AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .Select(o => new { o.Id, o.DisplayName, o.ShortName, o.Type, o.Status, o.LogoAssetId })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => new OrganisationSummary(
            row.Id, row.DisplayName, row.ShortName, Map(row.Type), Map(row.Status), row.LogoAssetId))];
    }

    private static OrganisationStatus Map(DomainStatus status) => status switch
    {
        DomainStatus.Active => OrganisationStatus.Active,
        DomainStatus.Inactive => OrganisationStatus.Inactive,
        DomainStatus.Suspended => OrganisationStatus.Suspended,
        DomainStatus.Dissolved => OrganisationStatus.Dissolved,
        DomainStatus.Archived => OrganisationStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static OrganisationType Map(DomainType type) => type switch
    {
        DomainType.Club => OrganisationType.Club,
        DomainType.School => OrganisationType.School,
        DomainType.University => OrganisationType.University,
        DomainType.Academy => OrganisationType.Academy,
        DomainType.Federation => OrganisationType.Federation,
        DomainType.Association => OrganisationType.Association,
        DomainType.Company => OrganisationType.Company,
        DomainType.Community => OrganisationType.Community,
        DomainType.Government => OrganisationType.Government,
        DomainType.Other => OrganisationType.Other,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
