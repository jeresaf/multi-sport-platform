using System.Globalization;

namespace SportsPlatform.Modules.Registry.Domain.People.Duplicates;

/// <summary>How sure the duplicate check is that an existing person is the candidate (S06 D5), weakest first.</summary>
public enum DuplicateConfidence
{
    Possible,
    Likely,

    /// <summary>An external identifier with the same authority and value: creation is blocked.</summary>
    ExactIdentifier,
}

// This summary is the schema's description in OpenAPI. SAME_DATE_OF_BIRTH discloses that much to whoever may run the
// check (open question Q42).
/// <summary>
/// Why a person matched. The answer never carries the other person's date of birth; <c>SAME_DATE_OF_BIRTH</c> confirms
/// a date the caller already sent.
/// </summary>
public enum DuplicateEvidenceCode
{
    /// <summary>Value: the identifier's authority.</summary>
    SameExternalId,
    SameDateOfBirth,
    BirthYearWithinOne,
    SameNameKey,

    /// <summary>Value: the similarity as a whole percentage.</summary>
    NameSimilarity,

    /// <summary>The same normalised last name, with the same date of birth (siblings and twins).</summary>
    SameLastName,
    SameNationality,

    /// <summary>The name matched one of the person's aliases, not the person's own name.</summary>
    AliasMatch,
}

public sealed record DuplicateEvidence(DuplicateEvidenceCode Code, string? Value = null);

/// <summary>The thresholds of the duplicate check (S06 D5): constants, not tenant settings.</summary>
public static class DuplicateThresholds
{
    /// <summary>With the same date of birth, a name-key similarity from here on is <c>LIKELY</c>.</summary>
    public const double Likely = 0.8;

    /// <summary>A name-key similarity from here on can be <c>POSSIBLE</c>; the check reads nobody below it by name.</summary>
    public const double Possible = 0.6;

    /// <summary>The check answers at most this many people, strongest first.</summary>
    public const int MaxCandidates = 20;
}

/// <summary>
/// What the check knows about one existing person next to the candidate. <paramref name="Similarity"/> is the best
/// trigram similarity of the candidate's name key to the person's name key or a live alias key (0–1);
/// <paramref name="SameNameKey"/> says one of them is equal; <paramref name="ViaAlias"/> that the best came from an alias.
/// </summary>
public sealed record DuplicateFacts(
    bool SameNameKey,
    double Similarity,
    bool ViaAlias,
    bool SameLastName,
    DateOnly? CandidateDateOfBirth,
    DateOnly? ExistingDateOfBirth,
    bool SameNationality,
    IReadOnlyList<string> SharedIdentifierAuthorities);

public sealed record DuplicateMatch(DuplicateConfidence Confidence, IReadOnlyList<DuplicateEvidence> Evidence);

/// <summary>
/// The rules of the duplicate check (S06 D5; the sibling rule by the owner, S06-T2a plan):
/// <list type="bullet">
/// <item><c>EXACT_IDENTIFIER</c>: a shared external identifier;</item>
/// <item><c>LIKELY</c>: the same date of birth and (the same name key, or similarity ≥ 0.8);</item>
/// <item><c>POSSIBLE</c>: similarity ≥ 0.6 and (a date of birth missing, or birth years within one year, or the same
/// nationality); or the same name key with another date of birth; or the same date of birth and the same last name.</item>
/// </list>
/// </summary>
public static class DuplicateMatcher
{
    public static DuplicateMatch? Match(DuplicateFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var similarity = facts.SameNameKey ? 1 : facts.Similarity;
        var bothBorn = facts.CandidateDateOfBirth is not null && facts.ExistingDateOfBirth is not null;
        var sameBirthDate = bothBorn && facts.CandidateDateOfBirth == facts.ExistingDateOfBirth;
        var yearsWithinOne = bothBorn
            && Math.Abs(facts.CandidateDateOfBirth!.Value.Year - facts.ExistingDateOfBirth!.Value.Year) <= 1;
        var nameIsClose = similarity >= DuplicateThresholds.Possible;
        var siblingRule = sameBirthDate && facts.SameLastName;

        DuplicateConfidence? confidence =
            facts.SharedIdentifierAuthorities.Count > 0 ? DuplicateConfidence.ExactIdentifier
            : sameBirthDate && similarity >= DuplicateThresholds.Likely ? DuplicateConfidence.Likely
            : facts.SameNameKey || siblingRule ? DuplicateConfidence.Possible
            : nameIsClose && (!bothBorn || yearsWithinOne || facts.SameNationality) ? DuplicateConfidence.Possible
            : null;
        if (confidence is null)
        {
            return null;
        }

        var evidence = new List<DuplicateEvidence>();
        evidence.AddRange(facts.SharedIdentifierAuthorities
            .Order(StringComparer.Ordinal)
            .Select(authority => new DuplicateEvidence(DuplicateEvidenceCode.SameExternalId, authority)));
        if (sameBirthDate)
        {
            evidence.Add(new(DuplicateEvidenceCode.SameDateOfBirth));
        }
        else if (yearsWithinOne)
        {
            evidence.Add(new(DuplicateEvidenceCode.BirthYearWithinOne));
        }

        if (facts.SameNameKey)
        {
            evidence.Add(new(DuplicateEvidenceCode.SameNameKey));
        }
        else if (nameIsClose)
        {
            // Rounded down, so 79.9 % never reads as the 80 % that would have been LIKELY.
            var percent = (int)Math.Floor((similarity * 100) + 1e-9);
            evidence.Add(new(DuplicateEvidenceCode.NameSimilarity, percent.ToString(CultureInfo.InvariantCulture)));
        }

        if (siblingRule && !facts.SameNameKey)
        {
            evidence.Add(new(DuplicateEvidenceCode.SameLastName));
        }

        if (nameIsClose && facts.ViaAlias)
        {
            evidence.Add(new(DuplicateEvidenceCode.AliasMatch));
        }

        if (facts.SameNationality)
        {
            evidence.Add(new(DuplicateEvidenceCode.SameNationality));
        }

        return new DuplicateMatch(confidence.Value, evidence);
    }
}
