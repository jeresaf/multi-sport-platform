using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People.UpdatePerson;

/// <summary>
/// Replaces a person's biodata (S06 D8), whatever their status (owner, S06-T3 plan: corrections stay allowed on an
/// archived or deceased person). The details' own rules come first, then the version. When the names, date of birth or
/// nationalities change (<see cref="Person.ChangesIdentity"/>), the update needs a duplicate check run for the new
/// details with <c>personId</c>, and runs it again holding <c>registry.people:&lt;tenantId&gt;</c> as a create does
/// (S06 D6): a person the token doesn't list is <c>PERSON_DUPLICATE_CHECK_STALE</c>; candidates without a reason are
/// <c>PERSON_DUPLICATE_OVERRIDE_REQUIRED</c>; with a reason each is remembered as not the same person. People already
/// decided not the same person as this one stay out of the check (D7; owner, Q45). Identifiers aren't changed here, so
/// the check compares none.
/// </summary>
public sealed class UpdatePersonHandler(
    RegistryDbContext context,
    DuplicateFinder finder,
    DuplicateCheckTokens tokens,
    TenantToday tenantToday,
    ITenantContext tenantContext,
    ICurrentActor actor,
    AuditScope audit,
    TimeProvider timeProvider)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid personId, UpdatePersonRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ifMatch);
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("A person is changed under the request's tenant.");

        // Described before anything can refuse, so a refused update is audited with its reason.
        audit.Describe(PeopleEndpoints.EntityType, personId, request.OverrideReason);
        if (PersonDetailsRules.Refuse(request, await tenantToday.GetAsync(cancellationToken)) is { } refusal)
        {
            return refusal;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var person = await context.Set<Person>().SingleOrDefaultAsync(p => p.Id == personId, cancellationToken);
        if (person is null)
        {
            return new(RegistryErrors.PersonNotFound);
        }

        if (!RowVersion.IsSatisfied(context.Entry(person).Property<uint>(AuditColumns.Version).CurrentValue, request.Version, ifMatch))
        {
            return new(RegistryErrors.ConcurrencyConflict);
        }

        var biodata = request.ToBiodata();
        if (person.ChangesIdentity(biodata) && await ReviewDuplicatesAsync(tenantId, person, request, cancellationToken) is { } stop)
        {
            return stop;
        }

        person.Update(biodata);
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return new(saved.Error);
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return null;
    }

    // The duplicate check of a change to the person's identity, as a create runs it (S06 D6, D8); null to go on.
    private async Task<FieldRefusal?> ReviewDuplicatesAsync(
        Guid tenantId, Person person, UpdatePersonRequest request, CancellationToken cancellationToken)
    {
        var input = PersonDetailsRules.ToCheckInput(request, person.Id);
        if (tokens.Read(request.CheckToken, input) is not { } checkedCandidates)
        {
            return new(RegistryErrors.DuplicateCheckRequired, "checkToken");
        }

        var key = $"registry.people:{tenantId}";
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);

        // Read after the lock: a new statement, so in READ COMMITTED it sees every person committed before it.
        var candidates = await finder.FindAsync(input, cancellationToken);
        audit.Describe(metadata: new DuplicateReviewAuditMetadata(
            [.. candidates.Select(candidate => new DuplicateCandidateAudit(candidate.PersonId, candidate.Match.Confidence))]));

        if (candidates.Any(candidate => !checkedCandidates.Contains(candidate.PersonId)))
        {
            return new(RegistryErrors.DuplicateCheckStale);
        }

        var reason = string.IsNullOrWhiteSpace(request.OverrideReason) ? null : request.OverrideReason.Trim();
        if (candidates.Count > 0 && reason is null)
        {
            return new(RegistryErrors.DuplicateOverrideRequired, "overrideReason");
        }

        var now = timeProvider.GetUtcNow();
        foreach (var candidate in candidates)
        {
            context.Add(DuplicateDecision.NotSame(person.Id, candidate.PersonId, reason!, actor, now));
        }

        return null;
    }
}
