using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats.Generation;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

namespace SportsPlatform.Modules.SportsConfiguration.Generators;

/// <summary>
/// A league (formats.md, "ROUND_ROBIN"): the circle method as de Werra's canonical schedule. With an odd number of
/// entrants a bye makes it even, and the bye takes the fixed place. Number the others 0…m−1 (m = entrants − 1, the
/// bye counted): round r pairs r with the fixed place, and r+k with r−k (mod m) for k = 1…entrants/2−1. With
/// <c>home_away_balance</c> the fixed place is at home in even rounds and r+k is at home when k is odd, which gives
/// each entrant at most one break in a cycle, so its home and away counts differ by at most one. Every second cycle
/// repeats the first with home and away swapped. <c>RANDOM</c> draws the order first; <c>SEEDED</c> and
/// <c>MANUAL</c> keep it.
/// </summary>
internal static class RoundRobinGenerator
{
    public static IReadOnlyList<GeneratedRound> Generate(
        IReadOnlyList<string> entrants, IReadOnlyDictionary<string, ParameterValue> parameters, SeededRandom random)
    {
        var cycles = parameters[FormatParameters.Cycles].Number;
        var balance = parameters[FormatParameters.HomeAwayBalance].Flag;
        var order = parameters[FormatParameters.Seeding].Choice == Seedings.Random ? random.Shuffle(entrants) : entrants;

        // Null is the bye; the last place is the fixed one.
        List<string?> places = [.. order];
        if (places.Count % 2 == 1)
        {
            places.Add(null);
        }

        var count = places.Count;
        var rotating = count - 1;
        var fixedPlace = places[count - 1];
        var cycle = new List<IReadOnlyList<(string? Home, string? Away)>>();
        for (var r = 0; r < rotating; r++)
        {
            var pairs = new List<(string?, string?)>
            {
                balance && r % 2 == 0 ? (fixedPlace, places[r]) : (places[r], fixedPlace),
            };
            for (var k = 1; k < count / 2; k++)
            {
                var first = places[(r + k) % rotating];
                var second = places[(r - k + rotating) % rotating];
                pairs.Add(!balance || k % 2 == 1 ? (first, second) : (second, first));
            }

            cycle.Add(pairs);
        }

        var rounds = new List<GeneratedRound>();
        for (var c = 0; c < cycles; c++)
        {
            foreach (var pairs in cycle)
            {
                var number = rounds.Count + 1;
                var code = RoundCodes.Round(number);
                var pairings = pairs
                    .Select((pair, index) => Pairing($"{code}_{index + 1}", c % 2 == 1 ? (pair.Away, pair.Home) : pair))
                    .ToList();
                rounds.Add(new GeneratedRound(number, code, BracketPlaces: null, pairings));
            }
        }

        return rounds;
    }

    private static GeneratedPairing Pairing(string code, (string? Home, string? Away) pair) => pair switch
    {
        (null, var entrant) => new GeneratedPairing(code, PairingSide.Entrant(entrant!), PairingSide.Bye),
        (var entrant, null) => new GeneratedPairing(code, PairingSide.Entrant(entrant), PairingSide.Bye),
        var (home, away) => new GeneratedPairing(code, PairingSide.Entrant(home), PairingSide.Entrant(away)),
    };
}
