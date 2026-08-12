using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines;

/// <summary>
/// Generates seeded or reproducibly randomized single-elimination draws, with
/// optional two-leg aggregate ties and a third-place playoff.
/// </summary>
public class KnockoutEngine : IKnockoutEngine
{
    public string FormatType => "Knockout";

    public IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        FormatOptions? options = null) =>
        GenerateContests(stage, participants, options as KnockoutOptions ?? new KnockoutOptions());

    public IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        KnockoutOptions? options = null)
    {
        options ??= new KnockoutOptions();
        var participantList = participants.ToList();
        if (participantList.Count < 2)
            throw new ArgumentException("At least 2 participants are required for knockout format.");
        if (!options.SportId.HasValue || options.SportId == Guid.Empty)
            throw new ArgumentException("A sport ID is required to generate knockout contests.");
        if (options.LegsPerTie is not 1 and not 2)
            throw new ArgumentOutOfRangeException(nameof(options.LegsPerTie), "Knockout ties support one or two legs.");

        var ordered = participantList
            .OrderBy(participant => participant.Seed ?? int.MaxValue)
            .ThenBy(participant => participant.ParticipantId)
            .ToList();
        if (options.RandomDraw)
            Shuffle(ordered, new Random(options.DrawSeed ?? Random.Shared.Next()));

        var bracketSize = 1;
        while (bracketSize < ordered.Count) bracketSize *= 2;
        var rounds = (int)Math.Log2(bracketSize);
        var results = new List<ContestGenerationResult>();
        var matchNumber = 1;

        for (var round = 1; round <= rounds; round++)
        {
            var ties = bracketSize / (int)Math.Pow(2, round);
            for (var tieIndex = 0; tieIndex < ties; tieIndex++)
            {
                var home = round == 1 ? ordered.ElementAtOrDefault(tieIndex) : null;
                var away = round == 1 ? ordered.ElementAtOrDefault(bracketSize - tieIndex - 1) : null;
                AddTie(
                    results,
                    stage,
                    options.SportId.Value,
                    round,
                    matchNumber++,
                    home,
                    away,
                    options.LegsPerTie,
                    isThirdPlace: false);
            }
        }

        if (options.IncludeThirdPlace && rounds >= 2)
        {
            AddTie(
                results,
                stage,
                options.SportId.Value,
                rounds,
                matchNumber,
                null,
                null,
                1,
                isThirdPlace: true);
        }

        return results;
    }

    public int CalculateRounds(int participantCount, bool includeThirdPlace) =>
        participantCount < 2 ? 0 : (int)Math.Ceiling(Math.Log2(participantCount));

    public int CalculateByes(int participantCount)
    {
        if (participantCount < 2) return 0;
        var size = 1;
        while (size < participantCount) size *= 2;
        return size - participantCount;
    }

    public IEnumerable<BracketPosition> GenerateSeededBracket(
        IEnumerable<ParticipantInfo> participants,
        int rounds)
    {
        var participantList = participants
            .OrderBy(participant => participant.Seed ?? int.MaxValue)
            .ThenBy(participant => participant.ParticipantId)
            .ToList();
        var bracket = new List<BracketPosition>();
        var totalPositions = (int)Math.Pow(2, rounds);
        var firstRoundMatches = totalPositions / 2;
        var seedPositions = GetStandardSeedPositions(firstRoundMatches);

        for (var index = 0; index < firstRoundMatches; index++)
        {
            var first = participantList.ElementAtOrDefault(seedPositions[index].Item1 - 1);
            var second = participantList.ElementAtOrDefault(seedPositions[index].Item2 - 1);
            bracket.Add(new BracketPosition(1, index, first?.ParticipantId, first is null, first?.Seed));
            bracket.Add(new BracketPosition(1, index + firstRoundMatches, second?.ParticipantId, second is null, second?.Seed));
        }

        for (var round = 2; round <= rounds; round++)
        {
            var matches = firstRoundMatches / (int)Math.Pow(2, round - 1);
            for (var index = 0; index < matches; index++)
                bracket.Add(new BracketPosition(round, index, null, false));
        }

        return bracket;
    }

    private static void AddTie(
        ICollection<ContestGenerationResult> results,
        Stage stage,
        Guid sportId,
        int round,
        int matchNumber,
        ParticipantInfo? home,
        ParticipantInfo? away,
        int legs,
        bool isThirdPlace)
    {
        var tieId = legs == 2 ? Guid.NewGuid() : (Guid?)null;
        for (var leg = 1; leg <= legs; leg++)
        {
            var first = leg == 1 ? home : away;
            var second = leg == 1 ? away : home;
            var contest = CreateContest(stage, sportId, matchNumber, first, second);
            if (tieId.HasValue) contest.AssignToAggregateTie(tieId.Value, leg);
            results.Add(new ContestGenerationResult(
                contest,
                round,
                matchNumber,
                first?.ParticipantId,
                second?.ParticipantId,
                TieId: tieId,
                LegNumber: leg,
                IsThirdPlace: isThirdPlace));
        }
    }

    private static Contest CreateContest(
        Stage stage,
        Guid sportId,
        int matchNumber,
        ParticipantInfo? home,
        ParticipantInfo? away)
    {
        var contest = new Contest(Guid.NewGuid(), stage.TenantId, sportId, "Knockout");
        contest.AssignToStage(stage.Id, matchNumber);
        if (home is not null) contest.AddParticipant(home.ParticipantId, home.ParticipantType, 1);
        if (away is not null) contest.AddParticipant(away.ParticipantId, away.ParticipantType, 2);
        return contest;
    }

    private static void Shuffle<T>(IList<T> values, Random random)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var other = random.Next(index + 1);
            (values[index], values[other]) = (values[other], values[index]);
        }
    }

    private static List<(int, int)> GetStandardSeedPositions(int firstRoundMatches) =>
        Enumerable.Range(0, firstRoundMatches)
            .Select(index => (index + 1, firstRoundMatches * 2 - index))
            .ToList();
}
