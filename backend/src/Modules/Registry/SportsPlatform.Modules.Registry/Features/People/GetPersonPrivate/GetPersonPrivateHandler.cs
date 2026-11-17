using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People.GetPersonPrivate;

/// <summary>
/// A person's <c>PRIVATE</c> data (S06 D3, D21). Saves nothing, so the Host writes one audit entry per read after the
/// response; the entry names the person by ID and carries no metadata.
/// </summary>
public sealed class GetPersonPrivateHandler(RegistryDbContext context, AuditScope audit)
{
    public async Task<Result<PersonPrivateResponse>> HandleAsync(Guid personId, CancellationToken cancellationToken)
    {
        audit.Describe(PeopleEndpoints.EntityType, personId);
        var person = await context.Set<Person>().AsNoTracking()
            .Where(p => p.Id == personId)
            .Select(p => new PersonPrivateResponse(p.Id, p.DateOfBirth))
            .SingleOrDefaultAsync(cancellationToken);
        return person is null ? RegistryErrors.PersonNotFound : Result.Success(person);
    }
}
