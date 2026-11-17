using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Registry.Infrastructure.Configurations;

namespace SportsPlatform.Modules.Registry.Features.People.CreatePerson;

/// <summary>
/// The audit metadata of a create (S06 D6): the people the check found, by ID, with their confidence. No names, no
/// dates; the reason to proceed is the entry's reason.
/// </summary>
public sealed record CreatePersonAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<DuplicateCandidateAudit> Candidates);

public sealed record DuplicateCandidateAudit(
    [property: PersonalData(PersonalDataClass.Internal)] Guid PersonId,
    [property: PersonalData(PersonalDataClass.Internal)] DuplicateConfidence Confidence);

/// <summary>
/// Creates a person after a duplicate check (S06 D6, D7). The details' own rules come first, then the token. In a
/// transaction holding <c>registry.people:&lt;tenantId&gt;</c>, the check runs again: a shared external identifier is
/// <c>PERSON_EXTERNAL_ID_EXISTS</c>; a person the token doesn't list (created since the check, or by the create this
/// token already made) is <c>PERSON_DUPLICATE_CHECK_STALE</c>; candidates without a reason are
/// <c>PERSON_DUPLICATE_OVERRIDE_REQUIRED</c>. With a reason, a "not the same person" decision is saved for each
/// candidate. Two creates of one person queue on the lock, and the second finds the first's person: no
/// <c>Idempotency-Key</c> is needed.
/// </summary>
public sealed class CreatePersonHandler(
    RegistryDbContext context,
    DuplicateFinder finder,
    DuplicateCheckTokens tokens,
    TenantToday tenantToday,
    ITenantContext tenantContext,
    ICurrentActor actor,
    AuditScope audit,
    TimeProvider timeProvider)
{
    public async Task<(Guid? PersonId, FieldRefusal? Refusal)> HandleAsync(CreatePersonRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("A person is created under the request's tenant.");

        // Described before anything can refuse, so a refused create is audited with its reason.
        audit.Describe(PeopleEndpoints.EntityType, reason: request.OverrideReason);
        if (PersonDetailsRules.Refuse(request, await tenantToday.GetAsync(cancellationToken)) is { } refusal)
        {
            return (null, refusal);
        }

        var input = PersonDetailsRules.ToCheckInput(request, personId: null);
        if (tokens.Read(request.CheckToken, input) is not { } checkedCandidates)
        {
            return (null, new(RegistryErrors.DuplicateCheckRequired, "checkToken"));
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var key = $"registry.people:{tenantId}";
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);

        // Read after the lock: a new statement, so in READ COMMITTED it sees every person committed before it.
        var candidates = await finder.FindAsync(input, cancellationToken);
        audit.Describe(metadata: new CreatePersonAuditMetadata(
            [.. candidates.Select(candidate => new DuplicateCandidateAudit(candidate.PersonId, candidate.Match.Confidence))]));

        if (candidates.Any(candidate => candidate.Match.Confidence == DuplicateConfidence.ExactIdentifier))
        {
            return (null, new(RegistryErrors.ExternalIdExists, await TakenIdentifierFieldAsync(input, cancellationToken)));
        }

        if (candidates.Any(candidate => !checkedCandidates.Contains(candidate.PersonId)))
        {
            return (null, new(RegistryErrors.DuplicateCheckStale));
        }

        var reason = string.IsNullOrWhiteSpace(request.OverrideReason) ? null : request.OverrideReason.Trim();
        if (candidates.Count > 0 && reason is null)
        {
            return (null, new(RegistryErrors.DuplicateOverrideRequired, "overrideReason"));
        }

        var now = timeProvider.GetUtcNow();
        var person = Person.Create(
            new PersonBiodata(
                request.FirstName!, request.LastName!, request.MiddleName, request.OtherNames, request.DisplayName,
                request.Gender, request.DateOfBirth, request.Nationality, request.SecondaryNationality, request.HeightCm,
                request.WeightKg),
            PersonSource.Manual,
            actor,
            now);
        context.Add(person);
        foreach (var identifier in request.ExternalIdentifiers ?? [])
        {
            context.Add(ExternalIdentifier.ForPerson(person.Id, identifier.Authority!, identifier.Value!, identifier.ValidFrom));
        }

        foreach (var candidate in candidates)
        {
            context.Add(DuplicateDecision.NotSame(person.Id, candidate.PersonId, reason!, actor, now));
        }

        audit.Describe(entityId: person.Id);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ExternalIdentifierConfiguration.ValueIndex,
        })
        {
            // Not reachable while every write of an identifier holds the lock; kept so a missed lock can't answer 500.
            return (null, new(RegistryErrors.ExternalIdExists));
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return (person.Id, null);
    }

    // The field of the first sent identifier another person already holds (ended ones included, S06 D10).
    private async Task<string?> TakenIdentifierFieldAsync(DuplicateCheckInput input, CancellationToken cancellationToken)
    {
        for (var i = 0; i < input.Identifiers.Count; i++)
        {
            var (authority, value) = input.Identifiers[i];
            if (await context.Set<ExternalIdentifier>().AnyAsync(
                    identifier => identifier.EntityType == ExternalIdentifierEntityType.Person
                        && identifier.Authority == authority && identifier.Value == value,
                    cancellationToken))
            {
                return $"externalIdentifiers[{i}].value";
            }
        }

        return null;
    }
}
