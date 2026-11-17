using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;

/// <summary>
/// What the duplicate check compares (S06 D5), normalised: the name key of every name part, the normalised last name,
/// and the identifiers with trimmed values. <paramref name="PersonId"/> is the person being changed, if any.
/// </summary>
public sealed record DuplicateCheckInput(
    string NameKey,
    string LastNameKey,
    DateOnly? DateOfBirth,
    string? Nationality,
    IReadOnlyList<(string Authority, string Value)> Identifiers,
    Guid? PersonId);

/// <summary>An existing person the check matched, with what the answer may show of them.</summary>
public sealed record DuplicateCandidate(
    Guid PersonId, string DisplayName, int? BirthYear, PersonStatus Status, DuplicateMatch Match, double Similarity);

/// <summary>
/// Finds the people of the scope's tenant (query filter and RLS) who may be the person in a
/// <see cref="DuplicateCheckInput"/> (S06 D5), strongest first, at most <see cref="DuplicateThresholds.MaxCandidates"/>.
/// People of every status are read (<c>MERGED</c> comes with merges, S25). Names are compared on the person's name key
/// and every alias that isn't removed; the best counts. Identifiers match on authority and value, ended ones included.
/// The existing person's date of birth is read to compare and goes no further than its year.
/// </summary>
public sealed class DuplicateFinder(RegistryDbContext context)
{
    // Rows read before classifying; identifier matches first, then the closest names. Far above what one name finds.
    private const int _maxRows = 200;

    public async Task<IReadOnlyList<DuplicateCandidate>> FindAsync(DuplicateCheckInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var authoritiesByPerson = await SharedIdentifiersAsync(input, cancellationToken);
        var identifierOwners = authoritiesByPerson.Keys.ToArray();
        var key = input.NameKey;
        var lastNameKey = input.LastNameKey;
        var dateOfBirth = input.DateOfBirth;
        var self = input.PersonId;

        var rows = await context.Set<Person>().AsNoTracking()
            .Where(p => self == null || p.Id != self)
            .Where(p =>
                p.NameKey == key
                || EF.Functions.TrigramsSimilarity(p.NameKey, key) >= DuplicateThresholds.Possible
                || p.Aliases.Any(a => a.RemovedAt == null
                    && (a.NameKey == key || EF.Functions.TrigramsSimilarity(a.NameKey, key) >= DuplicateThresholds.Possible))
                || (dateOfBirth != null && p.DateOfBirth == dateOfBirth && p.LastNameKey == lastNameKey)
                || identifierOwners.Contains(p.Id))
            .OrderByDescending(p => identifierOwners.Contains(p.Id))
            .ThenByDescending(p => p.NameKey == key)
            .ThenByDescending(p => EF.Functions.TrigramsSimilarity(p.NameKey, key))
            .ThenBy(p => p.Id)
            .Take(_maxRows)
            .Select(p => new Row(
                p.Id,
                p.DisplayName,
                p.DateOfBirth,
                p.Status,
                p.Nationality,
                p.NameKey == key,
                p.LastNameKey == lastNameKey,
                EF.Functions.TrigramsSimilarity(p.NameKey, key),
                p.Aliases.Any(a => a.RemovedAt == null && a.NameKey == key),
                p.Aliases.Where(a => a.RemovedAt == null).Max(a => (double?)EF.Functions.TrigramsSimilarity(a.NameKey, key)),
                identifierOwners.Contains(p.Id)))
            .ToListAsync(cancellationToken);

        var decided = await DecidedAsync(self, cancellationToken);
        var candidates = new List<DuplicateCandidate>();
        foreach (var row in rows.Where(row => !decided.Contains(row.Id)))
        {
            // pg_trgm answers a 4-byte float: 0.8 arrives as 0.800000011920929.
            var own = Math.Round(row.NameSimilarity, 4);
            var alias = Math.Round(row.AliasSimilarity ?? 0, 4);
            var viaAlias = !row.SameNameKey && (row.AliasSameNameKey || alias > own);
            var similarity = row.SameNameKey || row.AliasSameNameKey ? 1 : Math.Max(own, alias);
            var match = DuplicateMatcher.Match(new DuplicateFacts(
                row.SameNameKey || row.AliasSameNameKey,
                similarity,
                viaAlias,
                row.SameLastName,
                input.DateOfBirth,
                row.DateOfBirth,
                input.Nationality is not null && input.Nationality == row.Nationality,
                authoritiesByPerson.GetValueOrDefault(row.Id) ?? []));
            if (match is not null)
            {
                candidates.Add(new DuplicateCandidate(row.Id, row.DisplayName, row.DateOfBirth?.Year, row.Status, match, similarity));
            }
        }

        return
        [
            .. candidates
                .OrderByDescending(candidate => candidate.Match.Confidence)
                .ThenByDescending(candidate => candidate.Similarity)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.PersonId)
                .Take(DuplicateThresholds.MaxCandidates),
        ];
    }

    // The people who hold one of the input's identifiers, with the authorities they share.
    private async Task<Dictionary<Guid, List<string>>> SharedIdentifiersAsync(DuplicateCheckInput input, CancellationToken cancellationToken)
    {
        if (input.Identifiers.Count == 0)
        {
            return [];
        }

        var authorities = input.Identifiers.Select(identifier => identifier.Authority).Distinct().ToArray();
        var values = input.Identifiers.Select(identifier => identifier.Value).Distinct().ToArray();
        var found = await context.Set<ExternalIdentifier>().AsNoTracking()
            .Where(i => i.EntityType == ExternalIdentifierEntityType.Person && authorities.Contains(i.Authority) && values.Contains(i.Value))
            .Select(i => new { i.EntityId, i.Authority, i.Value })
            .ToListAsync(cancellationToken);

        var wanted = input.Identifiers.ToHashSet();
        return found
            .Where(i => wanted.Contains((i.Authority, i.Value)) && i.EntityId != input.PersonId)
            .GroupBy(i => i.EntityId)
            .ToDictionary(group => group.Key, group => group.Select(i => i.Authority).Distinct().ToList());
    }

    // Everyone already decided "not the same person" as the person being changed (S06 D7); nobody for a new person.
    private async Task<HashSet<Guid>> DecidedAsync(Guid? personId, CancellationToken cancellationToken)
    {
        if (personId is not { } id)
        {
            return [];
        }

        var pairs = await context.Set<DuplicateDecision>().AsNoTracking()
            .Where(d => d.PersonLowId == id || d.PersonHighId == id)
            .Select(d => new { d.PersonLowId, d.PersonHighId })
            .ToListAsync(cancellationToken);
        return [.. pairs.Select(pair => pair.PersonLowId == id ? pair.PersonHighId : pair.PersonLowId)];
    }

    private sealed record Row(
        Guid Id, string DisplayName, DateOnly? DateOfBirth, PersonStatus Status, string? Nationality, bool SameNameKey,
        bool SameLastName, double NameSimilarity, bool AliasSameNameKey, double? AliasSimilarity, bool OwnsIdentifier);
}
