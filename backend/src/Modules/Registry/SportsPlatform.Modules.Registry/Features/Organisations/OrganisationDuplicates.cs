using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Organisations;

/// <summary>The thresholds of the organisation duplicate warning (S07 D13): constants, not tenant settings.</summary>
public static class OrganisationDuplicateThresholds
{
    /// <summary>The trigram similarity of two display name keys from which they look alike (inclusive).</summary>
    public const double Similar = 0.7;

    public const int MaxCandidates = 10;
}

/// <summary>Why an organisation looks like the one checked (S07 D13).</summary>
public enum OrganisationDuplicateEvidenceCode
{
    /// <summary>
    /// One of the names (display, legal, short, or a current alias) has the same key as one of the other's: the same
    /// words in any order, case, accents and punctuation aside.
    /// </summary>
    SameNameKey,

    /// <summary>Display names at least <see cref="OrganisationDuplicateThresholds.Similar"/> similar; the value is the similarity.</summary>
    NameSimilarity,

    SameType,

    SameCity,
}

public sealed record OrganisationDuplicateEvidence(
    [property: PersonalData(PersonalDataClass.Public)] OrganisationDuplicateEvidenceCode Code,
    [property: PersonalData(PersonalDataClass.Public)] string? Value = null);

/// <summary>
/// What the warning compares, normalised: the display name's key, the legal and short names' keys (null when absent),
/// the type and the city key (null when there is none). <paramref name="OrganisationId"/> is the organisation being
/// changed, if any: its current aliases are compared too (S07 D13, "either side").
/// </summary>
public sealed record OrganisationDuplicateInput(
    string DisplayNameKey,
    string? LegalNameKey,
    string? ShortNameKey,
    OrganisationType Type,
    string? CityKey,
    Guid? OrganisationId)
{
    /// <summary>The keys of every name sent, without repeats.</summary>
    public IReadOnlyList<string> NameKeys =>
        [.. new[] { DisplayNameKey, LegalNameKey, ShortNameKey }.OfType<string>().Distinct(StringComparer.Ordinal)];
}

/// <summary>An existing organisation that looks like the one checked, strongest first.</summary>
public sealed record OrganisationDuplicateCandidate(
    Guid OrganisationId,
    string DisplayName,
    OrganisationType Type,
    string? City,
    OrganisationStatus Status,
    DuplicateConfidence Confidence,
    IReadOnlyList<OrganisationDuplicateEvidence> Evidence,
    double Similarity);

/// <summary>
/// Finds the organisations of the scope's tenant (query filter and RLS) that look like an
/// <see cref="OrganisationDuplicateInput"/> (S07 D13), archived ones included. Only organisations of the same type or
/// the same city are compared (a missing city never matches): <c>LIKELY</c> when a key of one (display, legal or short
/// name, or a current alias) equals a key of the other (owner, S07-T2d plan: aliases on both sides; the organisation
/// being changed brings its own aliases, a new one has none); <c>POSSIBLE</c> when the display names are at least 0.7
/// similar (owner, plan 2026-11-22: display names only). Strongest first, then the most similar display name, then by name; at most
/// <see cref="OrganisationDuplicateThresholds.MaxCandidates"/>.
/// </summary>
public sealed class OrganisationDuplicateFinder(RegistryDbContext context)
{
    // Rows read before classifying, in the query's order (same keys first, so a LIKELY is never cut for names that are
    // only similar). Far above what one name finds.
    public const int MaxRows = 100;

    public async Task<IReadOnlyList<OrganisationDuplicateCandidate>> FindAsync(
        OrganisationDuplicateInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var key = input.DisplayNameKey;
        var type = input.Type;
        var cityKey = input.CityKey;
        var self = input.OrganisationId;

        // The organisation being changed is known by its current aliases too.
        var ownAliasKeys = self is null
            ? []
            : await context.Set<OrganisationAlias>().AsNoTracking()
                .Where(a => a.OrganisationId == self && a.RemovedAt == null)
                .Select(a => a.NameKey)
                .ToListAsync(cancellationToken);
        var keys = input.NameKeys.Concat(ownAliasKeys).Distinct(StringComparer.Ordinal).ToList();

        // The type and the city narrow the rows first ((tenant_id, type) and (tenant_id, city_key) are indexed), so the
        // similarity is measured only within them.
        var rows = await context.Set<Organisation>().AsNoTracking()
            .Where(o => self == null || o.Id != self)
            .Where(o => o.Type == type || (cityKey != null && o.CityKey == cityKey))
            .Select(o => new
            {
                o.Id,
                o.DisplayName,
                o.Type,
                o.City,
                o.Status,
                SameNameKey = keys.Contains(o.DisplayNameKey)
                    || (o.LegalNameKey != null && keys.Contains(o.LegalNameKey))
                    || (o.ShortNameKey != null && keys.Contains(o.ShortNameKey))
                    || o.Aliases.Any(a => a.RemovedAt == null && keys.Contains(a.NameKey)),
                SameType = o.Type == type,
                SameCity = cityKey != null && o.CityKey == cityKey,
                Similarity = EF.Functions.TrigramsSimilarity(o.DisplayNameKey, key),
            })
            .Where(row => row.SameNameKey || row.Similarity >= OrganisationDuplicateThresholds.Similar - 0.0001)
            .OrderByDescending(row => row.SameNameKey)
            .ThenByDescending(row => row.Similarity)
            .ThenBy(row => row.Id)
            .Take(MaxRows)
            .Select(row => new Row(
                row.Id, row.DisplayName, row.Type, row.City, row.Status, row.SameNameKey, row.SameType, row.SameCity, row.Similarity))
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(Classify).OfType<OrganisationDuplicateCandidate>()
                .OrderByDescending(candidate => candidate.Confidence)
                .ThenByDescending(candidate => candidate.Similarity)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.OrganisationId)
                .Take(OrganisationDuplicateThresholds.MaxCandidates),
        ];
    }

    private static OrganisationDuplicateCandidate? Classify(Row row)
    {
        // pg_trgm answers a 4-byte float: 0.7 arrives as 0.699999988079071.
        var similarity = Math.Round(row.Similarity, 4);
        var similar = similarity >= OrganisationDuplicateThresholds.Similar;
        if (!(row.SameType || row.SameCity) || !(row.SameNameKey || similar))
        {
            return null;
        }

        var evidence = new List<OrganisationDuplicateEvidence>();
        if (row.SameNameKey)
        {
            evidence.Add(new(OrganisationDuplicateEvidenceCode.SameNameKey));
        }
        else
        {
            evidence.Add(new(OrganisationDuplicateEvidenceCode.NameSimilarity, similarity.ToString("0.00", CultureInfo.InvariantCulture)));
        }

        if (row.SameType)
        {
            evidence.Add(new(OrganisationDuplicateEvidenceCode.SameType));
        }

        if (row.SameCity)
        {
            evidence.Add(new(OrganisationDuplicateEvidenceCode.SameCity));
        }

        var confidence = row.SameNameKey ? DuplicateConfidence.Likely : DuplicateConfidence.Possible;
        return new OrganisationDuplicateCandidate(
            row.Id, row.DisplayName, row.Type, row.City, row.Status, confidence, evidence, row.SameNameKey ? 1 : similarity);
    }

    private sealed record Row(
        Guid Id,
        string DisplayName,
        OrganisationType Type,
        string? City,
        OrganisationStatus Status,
        bool SameNameKey,
        bool SameType,
        bool SameCity,
        double Similarity);
}
