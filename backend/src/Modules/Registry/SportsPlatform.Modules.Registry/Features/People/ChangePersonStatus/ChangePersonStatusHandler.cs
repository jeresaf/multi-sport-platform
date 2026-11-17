using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People.ChangePersonStatus;

/// <summary>
/// Moves a person along the lifecycle (S06 D9): the status-history row and the audit entry (with the reason) commit
/// together. A transition the current status doesn't allow is <c>PERSON_STATUS_INVALID</c>.
/// </summary>
public sealed class ChangePersonStatusHandler(RegistryDbContext context, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<Result> HandleAsync(
        Guid personId, PersonTransition transition, string? reason, uint? version, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ifMatch);
        audit.Describe(PeopleEndpoints.EntityType, personId, reason);

        var person = await context.Set<Person>().SingleOrDefaultAsync(p => p.Id == personId, cancellationToken);
        if (person is null)
        {
            return Result.Failure(RegistryErrors.PersonNotFound);
        }

        if (!RowVersion.IsSatisfied(context.Entry(person).Property<uint>(AuditColumns.Version).CurrentValue, version, ifMatch))
        {
            return Result.Failure(RegistryErrors.ConcurrencyConflict);
        }

        var changed = person.ChangeStatus(transition, reason, actor, timeProvider.GetUtcNow());
        if (changed.IsFailure)
        {
            return changed;
        }

        return await context.SaveChangesResultAsync(cancellationToken);
    }
}
