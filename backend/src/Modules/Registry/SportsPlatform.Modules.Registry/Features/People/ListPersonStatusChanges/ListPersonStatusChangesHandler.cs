using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People.ListPersonStatusChanges;

public sealed record ListPersonStatusChangesResponse(
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<PersonStatusChangeResponse> Items);

/// <summary>A person's whole status history, newest first (S06 D9, D22); a person has few rows, so it is not paged.</summary>
public sealed class ListPersonStatusChangesHandler(RegistryDbContext context)
{
    public async Task<Result<ListPersonStatusChangesResponse>> HandleAsync(Guid personId, CancellationToken cancellationToken)
    {
        if (!await context.Set<Person>().AnyAsync(p => p.Id == personId, cancellationToken))
        {
            return RegistryErrors.PersonNotFound;
        }

        var items = await context.Set<PersonStatusChange>().AsNoTracking()
            .Where(c => c.PersonId == personId)
            .OrderByDescending(c => c.OccurredAt).ThenByDescending(c => c.Id)
            .Select(c => new PersonStatusChangeResponse(c.Id, c.FromStatus, c.ToStatus, c.Reason, c.ActorKind, c.ActorId, c.OccurredAt))
            .ToListAsync(cancellationToken);
        return Result.Success(new ListPersonStatusChangesResponse(items));
    }
}
