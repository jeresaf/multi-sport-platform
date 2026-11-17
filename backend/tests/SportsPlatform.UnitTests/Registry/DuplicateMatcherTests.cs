using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using static SportsPlatform.Modules.Registry.Domain.People.Duplicates.DuplicateConfidence;
using static SportsPlatform.Modules.Registry.Domain.People.Duplicates.DuplicateEvidenceCode;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>The duplicate check's rules and thresholds (S06 D5; the sibling rule, S06-T2a).</summary>
public sealed class DuplicateMatcherTests
{
    private static readonly DateOnly _born = new(2001, 4, 12);

    public static TheoryData<string, DuplicateFacts, DuplicateConfidence?> Rules => new()
    {
        // LIKELY: the same date of birth and the same name key, or similarity from 0.8.
        { "same key, same birth date", Facts(sameKey: true, existing: _born), Likely },
        { "0.80, same birth date", Facts(similarity: 0.8, existing: _born), Likely },
        { "0.79, same birth date", Facts(similarity: 0.79, existing: _born), Possible },

        // POSSIBLE: similarity from 0.6 and a date missing, years within one, or the same nationality.
        { "0.60, same birth date", Facts(similarity: 0.6, existing: _born), Possible },
        { "0.59, same birth date", Facts(similarity: 0.59, existing: _born), null },
        { "0.60, candidate's date missing", Facts(similarity: 0.6, candidateBorn: false, existing: _born), Possible },
        { "0.60, existing date missing", Facts(similarity: 0.6, existing: null), Possible },
        { "0.60, both dates missing", Facts(similarity: 0.6, candidateBorn: false, existing: null), Possible },
        { "0.59, both dates missing", Facts(similarity: 0.59, candidateBorn: false, existing: null), null },
        { "0.99, both dates missing", Facts(similarity: 0.99, candidateBorn: false, existing: null), Possible },
        { "0.60, born the year after", Facts(similarity: 0.6, existing: new(2002, 12, 31)), Possible },
        { "0.60, born the year before", Facts(similarity: 0.6, existing: new(2000, 1, 1)), Possible },
        { "0.60, born two years apart", Facts(similarity: 0.6, existing: new(2003, 1, 1)), null },
        { "0.95, born two years apart", Facts(similarity: 0.95, existing: new(1999, 4, 12)), null },
        { "0.60, two years apart, same nationality", Facts(similarity: 0.6, existing: new(2003, 1, 1), sameNationality: true), Possible },
        { "0.59, same nationality", Facts(similarity: 0.59, existing: new(2003, 1, 1), sameNationality: true), null },

        // POSSIBLE: the same name key with another date of birth, however far apart.
        { "same key, born ten years apart", Facts(sameKey: true, existing: new(1991, 4, 12)), Possible },
        { "same key, date missing", Facts(sameKey: true, existing: null), Possible },

        // POSSIBLE, never LIKELY: twins and siblings share the birth date and the last name.
        { "0.33, same birth date and last name", Facts(similarity: 0.33, existing: _born, sameLastName: true), Possible },
        { "0.79, same birth date and last name", Facts(similarity: 0.79, existing: _born, sameLastName: true), Possible },
        { "0.33, same last name, another birth date", Facts(similarity: 0.33, existing: new(2001, 4, 13), sameLastName: true), null },
        { "0.33, same last name, date missing", Facts(similarity: 0.33, existing: null, sameLastName: true), null },
        { "0.33, same birth date only", Facts(similarity: 0.33, existing: _born), null },

        // EXACT_IDENTIFIER: whatever the names and dates say.
        { "shared identifier, nothing else", Facts(similarity: 0.1, existing: new(1980, 1, 1), authorities: ["KMSA_REG_NO"]), ExactIdentifier },
        { "shared identifier, same person", Facts(sameKey: true, existing: _born, authorities: ["KMSA_REG_NO"]), ExactIdentifier },
    };

    [Theory]
    [MemberData(nameof(Rules))]
    public void A_person_matches_by_the_rules_at_their_thresholds(string because, DuplicateFacts facts, DuplicateConfidence? expected)
    {
        var confidence = DuplicateMatcher.Match(facts)?.Confidence;

        confidence.ShouldBe(expected, because);
    }

    [Fact]
    public void Evidence_says_why_without_a_date_of_birth()
    {
        Evidence(Facts(sameKey: true, existing: _born, sameNationality: true))
            .ShouldBe([new(SameDateOfBirth), new(SameNameKey), new(SameNationality)]);
        Evidence(Facts(similarity: 0.6875, existing: new(2002, 1, 1), viaAlias: true))
            .ShouldBe([new(BirthYearWithinOne), new(NameSimilarity, "68"), new(AliasMatch)]);
        Evidence(Facts(similarity: 0.33, existing: _born, sameLastName: true))
            .ShouldBe([new(SameDateOfBirth), new(SameLastName)]);
        Evidence(Facts(similarity: 0.7, candidateBorn: false, existing: _born))
            .ShouldBe([new(NameSimilarity, "70")]);
        Evidence(Facts(similarity: 0.2, existing: new(1980, 1, 1), authorities: ["SCHOOL_ADMISSION_NO", "KMSA_REG_NO"]))
            .ShouldBe([new(SameExternalId, "KMSA_REG_NO"), new(SameExternalId, "SCHOOL_ADMISSION_NO")]);
    }

    [Theory]
    [InlineData(0.799, "79")] // never the 80 % that would have been LIKELY
    [InlineData(0.8, "80")]
    [InlineData(0.6, "60")]
    [InlineData(0.58, "58")]
    public void The_similarity_percentage_is_rounded_down(double similarity, string expected)
    {
        var facts = Facts(similarity: similarity, candidateBorn: false, existing: null, sameLastName: false) with { SameNationality = true };
        if (similarity < DuplicateThresholds.Possible)
        {
            DuplicateMatcher.Match(facts).ShouldBeNull();
            return;
        }

        Evidence(facts).ShouldContain(new DuplicateEvidence(NameSimilarity, expected));
    }

    [Fact]
    public void A_decision_stores_its_pair_in_one_order_whoever_is_named_first()
    {
        // .NET compares a Guid's first field as a signed number; PostgreSQL compares bytes. "8…" sorts after "7…" there.
        var low = new Guid("7fffffff-0000-7000-8000-000000000001");
        var high = new Guid("80000000-0000-7000-8000-000000000001");
        var now = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

        var decision = DuplicateDecision.NotSame(high, low, "  Twin sister, different first name ", SystemActor.Instance, now);

        (decision.PersonLowId, decision.PersonHighId).ShouldBe((low, high));
        DuplicateDecision.Ordered(low, high).ShouldBe((low, high));
        decision.OtherThan(low).ShouldBe(high);
        decision.OtherThan(high).ShouldBe(low);
        (decision.Decision, decision.Reason, decision.DecidedAt).ShouldBe((DuplicateDecisionKind.NotSame, "Twin sister, different first name", now));
        Should.Throw<ArgumentException>(() => DuplicateDecision.NotSame(low, low, "Same person twice", SystemActor.Instance, now));
        Should.Throw<ArgumentException>(() => DuplicateDecision.NotSame(low, high, " ", SystemActor.Instance, now));
        Should.Throw<ArgumentOutOfRangeException>(
            () => DuplicateDecision.NotSame(low, high, new string('x', DuplicateDecision.ReasonMaxLength + 1), SystemActor.Instance, now));
    }

    private static IReadOnlyList<DuplicateEvidence> Evidence(DuplicateFacts facts) => DuplicateMatcher.Match(facts)!.Evidence;

    // The candidate was born on _born unless candidateBorn is false; the existing person has no date unless given.
    private static DuplicateFacts Facts(
        bool sameKey = false, double similarity = 0, bool viaAlias = false, bool sameLastName = false,
        bool candidateBorn = true, DateOnly? existing = null, bool sameNationality = false, string[]? authorities = null) =>
        new(sameKey, similarity, viaAlias, sameLastName, candidateBorn ? _born : null, existing, sameNationality, authorities ?? []);
}
