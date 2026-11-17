using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Infrastructure;
using DomainStatus = SportsPlatform.Modules.Registry.Domain.People.PersonStatus;
using PersonStatus = SportsPlatform.Modules.Registry.Contracts.People.PersonStatus;

namespace SportsPlatform.Modules.Registry.Features.People;

/// <summary><see cref="IPeopleDirectory"/> over the scope's tenant (query filter and RLS).</summary>
public sealed class PeopleDirectory(RegistryDbContext context, TenantToday tenantToday) : IPeopleDirectory
{
    public async Task<IReadOnlyList<PersonSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(personIds);
        if (personIds.Count == 0)
        {
            return [];
        }

        var ids = personIds.Distinct().ToArray();
        var rows = await context.Set<Person>().AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.DisplayName, p.Status, p.DateOfBirth })
            .ToListAsync(cancellationToken);
        var today = await tenantToday.GetAsync(cancellationToken);
        return [.. rows.Select(row => new PersonSummary(row.Id, row.DisplayName, Map(row.Status), Age.StatusOn(row.DateOfBirth, today)))];
    }

    private static PersonStatus Map(DomainStatus status) => status switch
    {
        DomainStatus.Active => PersonStatus.Active,
        DomainStatus.Inactive => PersonStatus.Inactive,
        DomainStatus.Deceased => PersonStatus.Deceased,
        DomainStatus.Archived => PersonStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
