using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats.Generation;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using static SportsPlatform.UnitTests.SportsConfiguration.Generators.GeneratorTestData;

namespace SportsPlatform.UnitTests.SportsConfiguration.Generators;

// formats.md, "ROUND_ROBIN", and S05-T3's tests: n = 2…20 for each number of cycles.
public sealed class RoundRobinGeneratorTests
{
    public static TheoryData<int, int> Leagues()
    {
        var data = new TheoryData<int, int>();
        for (var n = 2; n <= 20; n++)
        {
            for (var cycles = 1; cycles <= 4; cycles++)
            {
                data.Add(n, cycles);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Leagues))]
    public void Every_pair_meets_once_per_cycle_and_each_entrant_plays_once_per_round(int n, int cycles)
    {
        var entrants = Entrants(n);

        var league = Generate(RoundRobin, Input(entrants, parameters: (FormatParameters.Cycles, ParameterValue.Of(cycles))));

        var rounds = league.Stages.ShouldHaveSingleItem().Rounds;
        var roundsPerCycle = n % 2 == 0 ? n - 1 : n;
        rounds.Count.ShouldBe(roundsPerCycle * cycles);
        rounds.Select(round => round.Number).ShouldBe(Enumerable.Range(1, rounds.Count));
        foreach (var round in rounds)
        {
            round.Code.ShouldBe($"ROUND_{round.Number}");
            round.Pairings.Count.ShouldBe((n + 1) / 2);
            round.Pairings.Count(pairing => pairing.IsBye).ShouldBe(n % 2);
            var sides = round.Pairings.SelectMany(pairing => new[] { pairing.Home, pairing.Away }).Where(side => side.Kind != PairingSideKind.Bye).ToList();
            sides.ShouldAllBe(side => side.Kind == PairingSideKind.Entrant);
            sides.Select(side => side.Reference).Order(StringComparer.Ordinal).ShouldBe(entrants);
        }

        var meetings = league.Contests().GroupBy(pairing => string.Join("|", new[] { pairing.Home.Reference, pairing.Away.Reference }.Order(StringComparer.Ordinal)));
        meetings.Count().ShouldBe(n * (n - 1) / 2);
        meetings.ShouldAllBe(meeting => meeting.Count() == cycles);
    }

    [Theory]
    [MemberData(nameof(Leagues))]
    public void Home_and_away_differ_by_at_most_one_in_each_cycle(int n, int cycles)
    {
        var league = Generate(RoundRobin, Input(Entrants(n), parameters: (FormatParameters.Cycles, ParameterValue.Of(cycles))));

        var roundsPerCycle = n % 2 == 0 ? n - 1 : n;
        foreach (var cycle in league.Stages[0].Rounds.Chunk(roundsPerCycle))
        {
            var contests = cycle.SelectMany(round => round.Pairings).Where(pairing => !pairing.IsBye).ToList();
            foreach (var entrant in Entrants(n))
            {
                var home = contests.Count(pairing => pairing.Home.Reference == entrant);
                var away = contests.Count(pairing => pairing.Away.Reference == entrant);
                (home + away).ShouldBe(n - 1);
                Math.Abs(home - away).ShouldBeLessThanOrEqualTo(1, $"{entrant} in a league of {n}");
            }
        }
    }

    [Fact]
    public void The_second_cycle_repeats_the_first_with_home_and_away_swapped()
    {
        var league = Generate(RoundRobin, Input(Clubs.Take(6).ToList(), parameters: (FormatParameters.Cycles, ParameterValue.Of(2))));

        var rounds = league.Stages[0].Rounds;
        for (var r = 0; r < 5; r++)
        {
            rounds[r + 5].Pairings.Select(pairing => (pairing.Home, pairing.Away))
                .ShouldBe(rounds[r].Pairings.Select(pairing => (pairing.Away, pairing.Home)));
        }
    }

    [Fact]
    public void A_bye_sits_away_and_names_no_contest()
    {
        var league = Generate(RoundRobin, Input(Clubs.Take(5).ToList()));

        var byes = league.Stages[0].Rounds.Select(round => round.Pairings.Single(pairing => pairing.IsBye)).ToList();
        byes.ShouldAllBe(bye => bye.Home.Kind == PairingSideKind.Entrant && bye.Away == PairingSide.Bye);
        byes.Select(bye => bye.Home.Reference).Order(StringComparer.Ordinal).ShouldBe(Clubs.Take(5).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Seeded_and_manual_keep_the_order_given_and_random_draws_it()
    {
        var clubs = Clubs.Take(4).ToList();
        GeneratedPairing First(string seeding, ulong seed) =>
            Generate(RoundRobin, Input(clubs, seed, (FormatParameters.Seeding, ParameterValue.Of(seeding)))).Stages[0].Rounds[0].Pairings[0];

        // Round 1 pairs the first entrant with the fixed place, the last; the fixed place is at home.
        First(Seedings.Seeded, 1).ShouldBe(new GeneratedPairing("ROUND_1_1", PairingSide.Entrant("KIREKA_KINGFISHERS"), PairingSide.Entrant("BUKOTO_COMETS")));
        First(Seedings.Manual, 1).ShouldBe(First(Seedings.Seeded, 2));
        Enumerable.Range(1, 10).Select(seed => First(Seedings.Random, (ulong)seed)).Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Without_balance_the_first_written_side_is_at_home()
    {
        var league = Generate(RoundRobin, Input(Clubs.Take(4).ToList(), parameters: [
            (FormatParameters.HomeAwayBalance, ParameterValue.Of(false)), (FormatParameters.Seeding, ParameterValue.Of(Seedings.Seeded))]));

        // Each round's first pairing is the rotating entrant at home against the fixed place.
        league.Stages[0].Rounds.Select(round => round.Pairings[0].Away.Reference).ShouldAllBe(away => away == "KIREKA_KINGFISHERS");
    }

    [Fact]
    public void A_stored_seed_reproduces_the_draw_and_its_hash()
    {
        var input = Input(Clubs.Take(7).ToList(), seed: 8_271_034_561_209UL);

        var first = Generate(RoundRobin, input);
        var again = Generate(RoundRobin, input with { Entrants = [.. input.Entrants] });

        again.OutputHash.ShouldBe(first.OutputHash);
        again.Stages[0].Rounds.SelectMany(round => round.Pairings).ShouldBe(first.Stages[0].Rounds.SelectMany(round => round.Pairings));

        // Another seed draws another order (RANDOM is the round robin's default), not only another hash.
        Generate(RoundRobin, input with { Seed = input.Seed + 1 }).Stages[0].Rounds.SelectMany(round => round.Pairings)
            .ShouldNotBe(first.Stages[0].Rounds.SelectMany(round => round.Pairings));
    }

    // S05-T3's pinned hash: a change to the generator, the PRNG or the hash's form fails here. The seed is above 2^53,
    // which a JSON number would round.
    [Fact]
    public void Seven_clubs_drawn_with_a_stored_seed_give_the_pinned_hash()
    {
        var league = Generate(RoundRobin, Input(Clubs.Take(7).ToList(), seed: 18_446_744_073_709_551_557UL));

        league.Parameters.ShouldBe(new Dictionary<string, ParameterValue>
        {
            [FormatParameters.Cycles] = ParameterValue.Of(1),
            [FormatParameters.HomeAwayBalance] = ParameterValue.Of(true),
            [FormatParameters.Seeding] = ParameterValue.Of(Seedings.Random),
        });
        league.OutputHash.ShouldBe("sha256:5db81839a765a30359b93b83f4dc7c19ba862541b4a2c1e459191fd3df302546");
    }
}
