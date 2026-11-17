using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People.Aliases;

/// <summary>
/// Adds an alias (S06 D10): no duplicate check (owner, Q45), refused on an archived or deceased person (D9) and when
/// the person is already known by the same name key (<c>PERSON_ALIAS_EXISTS</c>). The new alias row moves the person's
/// version (D11).
/// </summary>
public sealed class AddPersonAliasHandler(RegistryDbContext context, AuditScope audit)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid personId, AddPersonAliasRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        audit.Describe(PeopleEndpoints.EntityType, personId);
        if (NameNormaliser.NameKey(request.Name).Length == 0)
        {
            return new(RegistryErrors.NameInvalid, "name");
        }

        var (person, refusal) = await PersonChanges.LoadAsync(context, personId, request.Version, ifMatch, cancellationToken);
        if (person is null)
        {
            return refusal;
        }

        var added = person.AddAlias(request.Name!, request.Kind!.Value);
        if (added.IsFailure)
        {
            return new(added.Error, added.Error.Code == RegistryErrors.AliasExists.Code ? "name" : null);
        }

        audit.Describe(metadata: new PersonAliasAuditMetadata(added.Value.Id));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}

/// <summary>Removes an alias with a reason, in any status (S06 D10; owner, Q45); the alias stays with who, when and why.</summary>
public sealed class RemovePersonAliasHandler(RegistryDbContext context, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid personId, Guid aliasId, RemovePersonAliasRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = request.Reason!.Trim();
        audit.Describe(PeopleEndpoints.EntityType, personId, reason, new PersonAliasAuditMetadata(aliasId));

        var (person, refusal) = await PersonChanges.LoadAsync(context, personId, request.Version, ifMatch, cancellationToken);
        if (person is null)
        {
            return refusal;
        }

        var removed = person.RemoveAlias(aliasId, reason, actor, timeProvider.GetUtcNow());
        if (removed.IsFailure)
        {
            return new(removed.Error);
        }

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}

/// <summary>The audit metadata of an alias change: the alias by ID, never its name.</summary>
public sealed record PersonAliasAuditMetadata([property: PersonalData(PersonalDataClass.Internal)] Guid AliasId);
