using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Teams;

/// <summary>The thresholds of the team duplicate warning (S07 D13): constants, not tenant settings.</summary>
public static class TeamDuplicateThresholds
{
    /// <summary>The trigram similarity of two name keys from which they look alike (inclusive).</summary>
    public const double Similar = 0.7;

    public const int MaxCandidates = 10;
}

/// <summary>Why a team looks like the one checked (S07 D13). Every candidate has the same variant and category.</summary>
public enum TeamDuplicateEvidenceCode
{
    /// <summary>
    /// The name or short name of one has the same key as the name or short name of the other: the same words in any
    /// order, case, accents and punctuation aside.
    /// </summary>
    SameNameKey,

    /// <summary>Names at least <see cref="TeamDuplicateThresholds.Similar"/> similar; the value is the similarity.</summary>
    NameSimilarity,

    /// <summary>Both belong to the same organisation.</summary>
    SameOrganisation,
}

public sealed record TeamDuplicateEvidence(
    [property: PersonalData(PersonalDataClass.Public)] TeamDuplicateEvidenceCode Code,
    [property: PersonalData(PersonalDataClass.Public)] string? Value = null);

/// <summary>
/// What the warning compares, normalised: the name's key, the short name's key (null when absent), the variant, the
/// category and the organisation (null for an independent team). <paramref name="TeamId"/> is the team being changed,
/// if any (S07-T3b): it is left out.
/// </summary>
public sealed record TeamDuplicateInput(
    string NameKey,
    string? ShortNameKey,
    string Variant,
    TeamCategory Category,
    Guid? OrganisationId,
    Guid? TeamId)
{
    /// <summary>The keys of every name sent, without repeats.</summary>
    public IReadOnlyList<string> NameKeys =>
        [.. new[] { NameKey, ShortNameKey }.OfType<string>().Distinct(StringComparer.Ordinal)];
}

/// <summary>An existing team that looks like the one checked, strongest first.</summary>
public sealed record TeamDuplicateCandidate(
    Guid TeamId,
    string Name,
    string Variant,
    TeamCategory Category,
    Guid? OrganisationId,
    TeamStatus Status,
    DuplicateConfidence Confidence,
    IReadOnlyList<TeamDuplicateEvidence> Evidence,
    double Similarity);

/// <summary>
/// Finds the teams of the scope's tenant (query filter and RLS) that look like a <see cref="TeamDuplicateInput"/> (S07
/// D13), archived ones included. Only teams of the same variant and category are compared (owner, S07-T3a: a club's
/// other categories are other teams, so neither confidence warns about them): <c>LIKELY</c> when the name or short name
/// of one has a key of the other's; <c>POSSIBLE</c> when the names are at least 0.7 similar and they share an
/// organisation, or either has none. The variant narrows the rows first (<c>(tenant_id, variant)</c> is indexed), so
/// the similarity is measured only within them. Strongest first, then the most similar, then by name; at most
/// <see cref="TeamDuplicateThresholds.MaxCandidates"/>.
/// </summary>
public sealed class TeamDuplicateFinder(RegistryDbContext context)
{
    // Rows read before classifying, in the query's order (same keys first, so a LIKELY is never cut for names that are
    // only similar; the organisation rule is in the query, so a POSSIBLE isn't cut by other organisations' teams). Far
    // above what one name finds.
    public const int MaxRows = 100;

    public async Task<IReadOnlyList<TeamDuplicateCandidate>> FindAsync(TeamDuplicateInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var key = input.NameKey;
        var keys = input.NameKeys.ToList();
        var variant = input.Variant;
        var (gender, ageGroup, level) = input.Category;
        var organisationId = input.OrganisationId;
        var self = input.TeamId;

        var rows = await context.Set<Team>().AsNoTracking()
            .Where(t => t.Variant == variant && t.GenderCategory == gender && t.AgeGroup == ageGroup && t.Level == level)
            .Where(t => self == null || t.Id != self)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Variant,
                t.GenderCategory,
                t.AgeGroup,
                t.Level,
                t.OrganisationId,
                t.Status,
                SameNameKey = keys.Contains(t.NameKey) || (t.ShortNameKey != null && keys.Contains(t.ShortNameKey)),
                Similarity = EF.Functions.TrigramsSimilarity(t.NameKey, key),
            })
            .Where(row => row.SameNameKey
                || (row.Similarity >= TeamDuplicateThresholds.Similar - 0.0001
                    && (organisationId == null || row.OrganisationId == null || row.OrganisationId == organisationId)))
            .OrderByDescending(row => row.SameNameKey)
            .ThenByDescending(row => row.Similarity)
            .ThenBy(row => row.Id)
            .Take(MaxRows)
            .Select(row => new Row(
                row.Id,
                row.Name,
                row.Variant,
                new TeamCategory(row.GenderCategory, row.AgeGroup, row.Level),
                row.OrganisationId,
                row.Status,
                row.SameNameKey,
                row.Similarity))
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => Classify(row, organisationId)).OfType<TeamDuplicateCandidate>()
                .OrderByDescending(candidate => candidate.Confidence)
                .ThenByDescending(candidate => candidate.Similarity)
                .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.TeamId)
                .Take(TeamDuplicateThresholds.MaxCandidates),
        ];
    }

    private static TeamDuplicateCandidate? Classify(Row row, Guid? organisationId)
    {
        // pg_trgm answers a 4-byte float: 0.7 arrives as 0.699999988079071.
        var similarity = Math.Round(row.Similarity, 4);
        var similar = similarity >= TeamDuplicateThresholds.Similar;
        var sameOrganisation = organisationId is not null && row.OrganisationId == organisationId;
        var organisationsMatch = sameOrganisation || organisationId is null || row.OrganisationId is null;
        if (!row.SameNameKey && !(similar && organisationsMatch))
        {
            return null;
        }

        var evidence = new List<TeamDuplicateEvidence>();
        if (row.SameNameKey)
        {
            evidence.Add(new(TeamDuplicateEvidenceCode.SameNameKey));
        }
        else
        {
            evidence.Add(new(TeamDuplicateEvidenceCode.NameSimilarity, similarity.ToString("0.00", CultureInfo.InvariantCulture)));
        }

        if (sameOrganisation)
        {
            evidence.Add(new(TeamDuplicateEvidenceCode.SameOrganisation));
        }

        var confidence = row.SameNameKey ? DuplicateConfidence.Likely : DuplicateConfidence.Possible;
        return new TeamDuplicateCandidate(
            row.Id, row.Name, row.Variant, row.Category, row.OrganisationId, row.Status, confidence, evidence,
            row.SameNameKey ? 1 : similarity);
    }

    private sealed record Row(
        Guid Id,
        string Name,
        string Variant,
        TeamCategory Category,
        Guid? OrganisationId,
        TeamStatus Status,
        bool SameNameKey,
        double Similarity);
}
