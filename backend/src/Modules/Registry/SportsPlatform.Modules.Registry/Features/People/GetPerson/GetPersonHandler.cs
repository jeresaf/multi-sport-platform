using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People.GetPerson;

/// <summary>
/// One person of the request's tenant, whatever its status; another tenant's person is not found (RLS). The date of
/// birth is read to compute the birth year and the age status on the tenant's date, and goes no further (S06 D3, D13).
/// </summary>
public sealed class GetPersonHandler(RegistryDbContext context, TenantToday tenantToday)
{
    public async Task<Result<PersonResponse>> HandleAsync(Guid personId, CancellationToken cancellationToken)
    {
        var person = await context.Set<Person>().AsNoTracking()
            .Where(p => p.Id == personId)
            .Select(p => new
            {
                Person = p,
                Aliases = p.Aliases.Where(a => a.RemovedAt == null).OrderBy(a => a.Id)
                    .Select(a => new PersonAliasResponse(a.Id, a.Name, a.Kind)).ToList(),
                Version = EF.Property<uint>(p, AuditColumns.Version),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (person is null)
        {
            return RegistryErrors.PersonNotFound;
        }

        var identifiers = await context.Set<ExternalIdentifier>().AsNoTracking()
            .Where(i => i.EntityType == ExternalIdentifierEntityType.Person && i.EntityId == personId)
            .OrderBy(i => i.Authority).ThenBy(i => i.Id)
            .Select(i => new ExternalIdentifierResponse(i.Id, i.Authority, i.Value, i.ValidFrom, i.ValidTo))
            .ToListAsync(cancellationToken);

        var p = person.Person;
        var ageStatus = Age.StatusOn(p.DateOfBirth, await tenantToday.GetAsync(cancellationToken));
        return Result.Success(new PersonResponse(
            p.Id, p.FirstName, p.MiddleName, p.LastName, p.OtherNames, p.DisplayName, p.Gender, p.DateOfBirth?.Year,
            Age.IsTreatedAsMinor(ageStatus), ageStatus, p.Nationality, p.SecondaryNationality, p.HeightCm, p.WeightKg,
            p.PublicProfileEnabled, p.Status, p.Source, person.Aliases, identifiers, person.Version));
    }
}
