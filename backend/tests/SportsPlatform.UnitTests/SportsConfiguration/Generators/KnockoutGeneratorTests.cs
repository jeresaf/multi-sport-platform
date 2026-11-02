using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats.Generation;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using static SportsPlatform.UnitTests.SportsConfiguration.Generators.GeneratorTestData;

namespace SportsPlatform.UnitTests.SportsConfiguration.Generators;

// formats.md, "KNOCKOUT", and S05-T3's tests: n = 2…64, with and without the third-place contest.
public sealed class KnockoutGeneratorTests
{
    public static TheoryData<int, bool> Brackets()
    {
        var data = new TheoryData<int, bool>();
        for (var n = 2; n <= 64; n++)
        {
            data.Add(n, false);
            if (n >= 4)
            {
                data.Add(n, true);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Brackets))]
    public void The_bracket_has_byes_only_for_the_top_seeds_and_every_slot_points_back(int n, bool thirdPlace)
    {
        var entrants = Entrants(n);

        var bracket = Generate(Knockout, Input(entrants, parameters: (FormatParameters.ThirdPlaceContest, ParameterValue.Of(thirdPlace))));

        var size = 2;
        while (size < n)
        {
            size *= 2;
        }

        var rounds = bracket.Stages.ShouldHaveSingleItem().Rounds;
        rounds.Select(round => round.Number).ShouldBe(Enumerable.Range(1, rounds.Count));
        var bracketRounds = rounds.Where(round => round.Code != RoundCodes.ThirdPlace).ToList();
        bracketRounds.Select(round => round.RemainingEntrants).ShouldBe(Enumerable.Range(0, bracketRounds.Count).Select(r => (int?)(size >> r)));
        bracketRounds[^1].Code.ShouldBe(RoundCodes.Final);
        rounds[^1].Code.ShouldBe(RoundCodes.Final);

        // Byes: only in round 1, one per missing line, each to one of the top seeds.
        var first = rounds[0].Pairings;
        first.Count.ShouldBe(size / 2);
        first.Where(pairing => pairing.IsBye).Select(pairing => pairing.Home.Reference).Order(StringComparer.Ordinal)
            .ShouldBe(entrants.Take(size - n));
        rounds.Skip(1).SelectMany(round => round.Pairings).ShouldAllBe(pairing => !pairing.IsBye);

        // Every entrant is in round 1 once; a bye's entrant is already in round 2; other slots name earlier pairings.
        first.SelectMany(pairing => new[] { pairing.Home, pairing.Away }).Where(side => side.Kind == PairingSideKind.Entrant)
            .Select(side => side.Reference).Order(StringComparer.Ordinal).ShouldBe(entrants);
        var earlier = new HashSet<string>(first.Where(pairing => !pairing.IsBye).Select(pairing => pairing.Code));
        var fed = new List<string>();
        foreach (var round in rounds.Skip(1))
        {
            foreach (var pairing in round.Pairings)
            {
                pairing.Code.ShouldBe($"{round.Code}_{round.Pairings.ToList().IndexOf(pairing) + 1}");
                foreach (var side in new[] { pairing.Home, pairing.Away })
                {
                    if (side.Kind == PairingSideKind.Entrant)
                    {
                        round.Number.ShouldBe(2);
                        first.ShouldContain(bye => bye.IsBye && bye.Home == side);
                    }
                    else
                    {
                        side.Kind.ShouldBeOneOf(PairingSideKind.WinnerOf, PairingSideKind.LoserOf);
                        earlier.ShouldContain(side.Reference!);
                        fed.Add($"{side.Kind}:{side.Reference}");
                    }
                }
            }

            earlier.UnionWith(round.Pairings.Select(pairing => pairing.Code));
        }

        // Each pairing feeds once (the semi-finals twice with a third-place contest); the final feeds nothing.
        fed.ShouldBeUnique();
        bracket.Contests().Count().ShouldBe(n - 1 + (thirdPlace ? 1 : 0));
    }

    [Theory]
    [MemberData(nameof(Brackets))]
    public void Seeds_one_and_two_can_meet_only_in_the_final(int n, bool thirdPlace)
    {
        var entrants = Entrants(n);

        var bracket = Generate(Knockout, Input(entrants, parameters: (FormatParameters.ThirdPlaceContest, ParameterValue.Of(thirdPlace))));

        // Follow each seed up the bracket, by the slots: the pairings seeds 1 and 2 can play in meet only at the final.
        var pairings = bracket.Stages[0].Rounds.Where(round => round.Code != RoundCodes.ThirdPlace).SelectMany(round => round.Pairings).ToList();
        var parent = pairings
            .SelectMany(pairing => new[] { pairing.Home, pairing.Away }.Where(side => side.Kind == PairingSideKind.WinnerOf).Select(side => (Child: side.Reference!, Parent: pairing.Code)))
            .ToDictionary(link => link.Child, link => link.Parent, StringComparer.Ordinal);
        HashSet<string> Path(string entrant)
        {
            var at = pairings.First(pairing => !pairing.IsBye && (pairing.Home == PairingSide.Entrant(entrant) || pairing.Away == PairingSide.Entrant(entrant))).Code;
            var path = new HashSet<string>(StringComparer.Ordinal) { at };
            while (parent.TryGetValue(at, out var next))
            {
                path.Add(at = next);
            }

            return path;
        }

        var one = Path(entrants[0]);
        var two = Path(entrants[1]);
        one.Intersect(two).ShouldBe(["FINAL_1"]);
        if (n >= 4)
        {
            // Seeds 1 to 4 meet each other no earlier than the semi-finals.
            for (var a = 0; a < 4; a++)
            {
                for (var b = a + 1; b < 4; b++)
                {
                    Path(entrants[a]).Intersect(Path(entrants[b])).ShouldAllBe(code => code.StartsWith(RoundCodes.SemiFinals, StringComparison.Ordinal) || code == "FINAL_1");
                }
            }
        }
    }

    [Fact]
    public void Eight_clubs_seeded_meet_in_the_standard_order()
    {
        var bracket = Generate(Knockout, Input(Clubs));

        bracket.Stages[0].Rounds.Select(round => round.Code).ShouldBe([RoundCodes.QuarterFinals, RoundCodes.SemiFinals, RoundCodes.Final]);
        bracket.Stages[0].Rounds[0].Pairings.Select(pairing => $"{pairing.Code}: {pairing.Home.Reference} v {pairing.Away.Reference}").ShouldBe([
            "QUARTER_FINALS_1: BUKOTO_COMETS v MUTUNDWE_METEORS",
            "QUARTER_FINALS_2: KIREKA_KINGFISHERS v BUNGA_BREAKERS",
            "QUARTER_FINALS_3: KANSANGA_HARRIERS v KYEBANDO_RAVENS",
            "QUARTER_FINALS_4: NAKAWA_NOMADS v SEETA_SPARROWS",
        ]);
        bracket.Stages[0].Rounds[1].Pairings.ShouldBe([
            new GeneratedPairing("SEMI_FINALS_1", PairingSide.WinnerOf("QUARTER_FINALS_1"), PairingSide.WinnerOf("QUARTER_FINALS_2")),
            new GeneratedPairing("SEMI_FINALS_2", PairingSide.WinnerOf("QUARTER_FINALS_3"), PairingSide.WinnerOf("QUARTER_FINALS_4")),
        ]);
    }

    [Fact]
    public void Five_clubs_give_byes_to_the_top_three_who_go_straight_to_the_semi_finals()
    {
        var bracket = Generate(Knockout, Input(Clubs.Take(5).ToList()));

        var rounds = bracket.Stages[0].Rounds;
        rounds[0].Pairings.Count(pairing => pairing.IsBye).ShouldBe(3);
        rounds[1].Pairings.ShouldBe([
            new GeneratedPairing("SEMI_FINALS_1", PairingSide.Entrant("BUKOTO_COMETS"), PairingSide.WinnerOf("QUARTER_FINALS_2")),
            new GeneratedPairing("SEMI_FINALS_2", PairingSide.Entrant("KANSANGA_HARRIERS"), PairingSide.Entrant("NAKAWA_NOMADS")),
        ]);
    }

    [Fact]
    public void The_third_place_contest_is_its_own_round_before_the_final()
    {
        var bracket = Generate(Knockout, Input(Clubs.Take(4).ToList(), parameters: (FormatParameters.ThirdPlaceContest, ParameterValue.Of(true))));

        var rounds = bracket.Stages[0].Rounds;
        rounds.Select(round => (round.Number, round.Code, round.RemainingEntrants)).ShouldBe([
            (1, RoundCodes.SemiFinals, (int?)4), (2, RoundCodes.ThirdPlace, null), (3, RoundCodes.Final, 2)]);
        rounds[1].Pairings.ShouldBe([new GeneratedPairing("THIRD_PLACE_1", PairingSide.LoserOf("SEMI_FINALS_1"), PairingSide.LoserOf("SEMI_FINALS_2"))]);
    }

    [Fact]
    public void Random_draws_the_seeds_with_the_stored_seed()
    {
        FormatGenerationInput Draw(ulong seed) => Input(Clubs, seed, (FormatParameters.Seeding, ParameterValue.Of(Seedings.Random)));

        var first = Generate(Knockout, Draw(5));

        Generate(Knockout, Draw(5)).ShouldSatisfyAllConditions(
            again => again.OutputHash.ShouldBe(first.OutputHash),
            again => again.Stages[0].Rounds[0].Pairings.ShouldBe(first.Stages[0].Rounds[0].Pairings));
        Enumerable.Range(6, 10).Select(seed => Generate(Knockout, Draw((ulong)seed)).Stages[0].Rounds[0].Pairings[0]).Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Manual_takes_the_bracket_lines_with_their_byes()
    {
        string?[] lines = ["SEETA_SPARROWS", null, "BUKOTO_COMETS", "NAKAWA_NOMADS", null, "KANSANGA_HARRIERS", "BUNGA_BREAKERS", "KIREKA_KINGFISHERS"];

        var bracket = Generate(Knockout, Input(lines, parameters: (FormatParameters.Seeding, ParameterValue.Of(Seedings.Manual))));

        bracket.Entrants.ShouldBe(lines);
        bracket.Stages[0].Rounds[0].Pairings.Select(pairing => (pairing.Home, pairing.Away)).ShouldBe([
            (PairingSide.Entrant("SEETA_SPARROWS"), PairingSide.Bye),
            (PairingSide.Entrant("BUKOTO_COMETS"), PairingSide.Entrant("NAKAWA_NOMADS")),
            (PairingSide.Entrant("KANSANGA_HARRIERS"), PairingSide.Bye),
            (PairingSide.Entrant("BUNGA_BREAKERS"), PairingSide.Entrant("KIREKA_KINGFISHERS")),
        ]);
        bracket.Stages[0].Rounds[1].Pairings[1].Home.ShouldBe(PairingSide.Entrant("KANSANGA_HARRIERS"));
    }

    [Fact]
    public void The_standard_seed_lines_for_sixteen()
    {
        var lines = Generate(Knockout, Input(Entrants(16))).Stages[0].Rounds[0].Pairings
            .SelectMany(pairing => new[] { pairing.Home.Reference!, pairing.Away.Reference! })
            .Select(key => int.Parse(key["ENTRANT_".Length..], System.Globalization.CultureInfo.InvariantCulture));

        lines.ShouldBe([1, 16, 8, 9, 4, 13, 5, 12, 2, 15, 7, 10, 3, 14, 6, 11]);
    }
}
