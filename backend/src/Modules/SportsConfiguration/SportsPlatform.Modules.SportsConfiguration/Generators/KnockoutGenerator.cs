using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats.Generation;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

namespace SportsPlatform.Modules.SportsConfiguration.Generators;

/// <summary>
/// Single elimination (formats.md, "KNOCKOUT"). The bracket has the next power of two of lines. <c>SEEDED</c> takes the
/// order given as seeds 1…n and <c>RANDOM</c> draws that order; both place the seeds in the standard order (1 v 16,
/// 8 v 9, …: seeds 1 and 2 can meet only in the final, 1 to 4 only from the semi-finals), and the missing seeds are
/// byes, so the byes go to the top seeds. <c>MANUAL</c> takes the bracket lines as given. A bye pairing makes no
/// contest and its entrant is already in the next round's slot; other slots are the winners of the pairings that feed
/// them. The third-place contest, losers of the semi-finals, is a round of its own before the final.
/// </summary>
internal static class KnockoutGenerator
{
    /// <summary>The bracket size for <paramref name="entrants"/> participants: the next power of two.</summary>
    public static int BracketSize(int entrants)
    {
        var size = 2;
        while (size < entrants)
        {
            size *= 2;
        }

        return size;
    }

    /// <summary>The seeds in their bracket lines: <c>1, 8, 4, 5, 2, 7, 3, 6</c> for 8.</summary>
    public static IReadOnlyList<int> SeedLines(int size)
    {
        // ToArray, not a spread into a list: that builds the list through CollectionsMarshal (InteropServices),
        // which the purity rule refuses.
        int[] lines = [1];
        while (lines.Length < size)
        {
            var next = (lines.Length * 2) + 1;
            lines = lines.SelectMany(seed => new[] { seed, next - seed }).ToArray();
        }

        return lines;
    }

    /// <summary>
    /// The rounds for <paramref name="lines"/>: the bracket lines in order, null for a bye, no pairing of two byes
    /// (the caller has checked).
    /// </summary>
    public static IReadOnlyList<GeneratedRound> Generate(IReadOnlyList<string?> lines, bool thirdPlaceContest)
    {
        var size = lines.Count;
        var rounds = new List<GeneratedRound>();

        // The sides that go into the next round: an entrant through a bye, or the winner of a pairing.
        var feeding = new List<PairingSide>();
        var firstRoundCode = RoundCodes.ForRemaining(size);
        var firstRound = new List<GeneratedPairing>();
        for (var i = 0; i < size / 2; i++)
        {
            var code = $"{firstRoundCode}_{i + 1}";
            var pairing = (lines[2 * i], lines[(2 * i) + 1]) switch
            {
                (null, var entrant) => new GeneratedPairing(code, PairingSide.Entrant(entrant!), PairingSide.Bye),
                (var entrant, null) => new GeneratedPairing(code, PairingSide.Entrant(entrant), PairingSide.Bye),
                var (home, away) => new GeneratedPairing(code, PairingSide.Entrant(home), PairingSide.Entrant(away)),
            };
            firstRound.Add(pairing);
            feeding.Add(pairing.IsBye ? pairing.Home : PairingSide.WinnerOf(code));
        }

        rounds.Add(new GeneratedRound(1, firstRoundCode, size, firstRound));

        for (var remaining = size / 2; remaining >= 2; remaining /= 2)
        {
            var roundCode = RoundCodes.ForRemaining(remaining);
            if (remaining == 2 && thirdPlaceContest)
            {
                var semiFinals = rounds[^1].Pairings;
                rounds.Add(new GeneratedRound(rounds.Count + 1, RoundCodes.ThirdPlace, RemainingEntrants: null,
                [
                    new GeneratedPairing(
                        $"{RoundCodes.ThirdPlace}_1", PairingSide.LoserOf(semiFinals[0].Code), PairingSide.LoserOf(semiFinals[1].Code)),
                ]));
            }

            var pairings = new List<GeneratedPairing>();
            var next = new List<PairingSide>();
            for (var i = 0; i < remaining / 2; i++)
            {
                var code = $"{roundCode}_{i + 1}";
                pairings.Add(new GeneratedPairing(code, feeding[2 * i], feeding[(2 * i) + 1]));
                next.Add(PairingSide.WinnerOf(code));
            }

            rounds.Add(new GeneratedRound(rounds.Count + 1, roundCode, remaining, pairings));
            feeding = next;
        }

        return rounds;
    }

    /// <summary>The bracket lines for <paramref name="entrants"/> as seeds 1…n, in their standard places.</summary>
    public static IReadOnlyList<string?> PlaceSeeds(IReadOnlyList<string> entrants)
    {
        var size = BracketSize(entrants.Count);
        return SeedLines(size).Select(seed => seed <= entrants.Count ? entrants[seed - 1] : null).ToArray();
    }

    /// <summary>The bracket lines for <paramref name="entrants"/> by the <c>seeding</c> parameter.</summary>
    public static IReadOnlyList<string?> Lines(
        IReadOnlyList<string?> entrants, IReadOnlyDictionary<string, ParameterValue> parameters, SeededRandom random) =>
        parameters[FormatParameters.Seeding].Choice switch
        {
            Seedings.Manual => entrants,
            Seedings.Random => PlaceSeeds(random.Shuffle(entrants.Select(entrant => entrant!).ToArray())),
            _ => PlaceSeeds(entrants.Select(entrant => entrant!).ToArray()),
        };
}
