using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People;

/// <summary>
/// What the commands on a person's aliases and identifiers share (S06-T3b): loading the person with its aliases at the
/// version the client read, the lock every write of an identifier holds, and the answer (the person with its
/// <c>ETag</c>, as the lifecycle actions answer).
/// </summary>
internal static class PersonChanges
{
    /// <summary>
    /// The person with its aliases (and its athlete profiles when <paramref name="withAthleteProfiles"/>), or
    /// <c>PERSON_NOT_FOUND</c>, or <c>PERSON_CONCURRENCY_CONFLICT</c> when <paramref name="version"/> or
    /// <paramref name="ifMatch"/> names another version.
    /// </summary>
    public static async Task<(Person? Person, FieldRefusal? Refusal)> LoadAsync(
        RegistryDbContext context, Guid personId, uint? version, IfMatch ifMatch, CancellationToken cancellationToken,
        bool withAthleteProfiles = false)
    {
        var people = context.Set<Person>().Include(p => p.Aliases).AsQueryable();
        if (withAthleteProfiles)
        {
            people = people.Include(p => p.AthleteProfiles).AsSplitQuery();
        }

        var person = await people.SingleOrDefaultAsync(p => p.Id == personId, cancellationToken);
        if (person is null)
        {
            return (null, new(RegistryErrors.PersonNotFound));
        }

        return RowVersion.IsSatisfied(context.Entry(person).Property<uint>(AuditColumns.Version).CurrentValue, version, ifMatch)
            ? (person, null)
            : (null, new(RegistryErrors.ConcurrencyConflict));
    }

    /// <summary>
    /// Takes <c>registry.people:&lt;tenantId&gt;</c> until the transaction ends: creates, identity updates and new
    /// identifiers hold it, so two of them can't both miss each other's people or identifiers (S06 D6).
    /// </summary>
    public static async Task LockPeopleAsync(RegistryDbContext context, Guid tenantId, CancellationToken cancellationToken)
    {
        var key = $"registry.people:{tenantId}";
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }

    /// <summary>Reads <c>If-Match</c>, runs <paramref name="change"/>, and answers the refusal or the saved person.</summary>
    public static async Task<Results<Ok<PersonResponse>, ProblemHttpResult>> AnswerAsync(
        Guid personId,
        GetPersonHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        Func<IfMatch, Task<FieldRefusal?>> change)
    {
        if (!RowVersion.TryReadIfMatch(httpRequest, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        if (await change(ifMatch) is { } refusal)
        {
            return refusal.ToProblem();
        }

        // Saved: the read is not cancelled with the request.
        var person = await read.HandleAsync(personId, CancellationToken.None);
        person.Value.SetETag(response);
        return TypedResults.Ok(person.Value);
    }
}
