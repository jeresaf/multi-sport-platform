using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines;

/// <summary>
/// Implementation of the round-robin format engine.
/// Generates contests where each participant plays against every other participant.
/// </summary>
public class RoundRobinEngine : IRoundRobinEngine
{
    public string FormatType => "RoundRobin";

    public IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        FormatOptions? options = null)
    {
        var roundRobinOptions = options as RoundRobinOptions ?? new RoundRobinOptions();
        return GenerateContests(stage, participants, roundRobinOptions);
    }

    public IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        RoundRobinOptions? options = null)
    {
        options ??= new RoundRobinOptions();
        var participantList = participants.ToList();
        
        if (participantList.Count < 2)
        {
            throw new ArgumentException("At least 2 participants are required for round-robin format.");
        }
        if (!options.SportId.HasValue || options.SportId == Guid.Empty)
        {
            throw new ArgumentException("A sport ID is required to generate round-robin contests.");
        }

        if (options.DrawSeed.HasValue)
            Shuffle(participantList, options.DrawSeed.Value);

        var results = new List<ContestGenerationResult>();
        var matchNumber = 1;

        // Use circle method for round-robin scheduling
        var rounds = GenerateRoundRobinSchedule(participantList, options.HomeAndAway);

        foreach (var round in rounds)
        {
            foreach (var matchup in round.Matchups)
            {
                var homeParticipant = participantList.FirstOrDefault(p => p.ParticipantId == matchup.HomeId);
                var awayParticipant = participantList.FirstOrDefault(p => p.ParticipantId == matchup.AwayId);

                var contest = CreateContest(
                    stage,
                    options.SportId.Value,
                    matchup.Round,
                    matchNumber,
                    matchup.HomeId,
                    matchup.AwayId,
                    homeParticipant?.ParticipantType,
                    awayParticipant?.ParticipantType);

                results.Add(new ContestGenerationResult(
                    Contest: contest,
                    Round: matchup.Round,
                    MatchNumber: matchNumber,
                    HomeParticipantId: matchup.HomeId,
                    AwayParticipantId: matchup.AwayId,
                    GroupName: null));

                matchNumber++;
            }
        }

        return results;
    }

    public int CalculateRounds(int participantCount, bool doubleRoundRobin)
    {
        if (participantCount < 2)
            return 0;

        // For odd number of participants, we need one more round
        var baseRounds = participantCount % 2 == 0 
            ? participantCount - 1 
            : participantCount;

        return doubleRoundRobin ? baseRounds * 2 : baseRounds;
    }

    private List<RoundMatchups> GenerateRoundRobinSchedule(List<ParticipantInfo> participants, bool homeAndAway)
    {
        var results = new List<RoundMatchups>();
        var n = participants.Count;
        var isOdd = n % 2 != 0;
        var teams = new List<ParticipantInfo?>(participants);

        // Add bye for odd number of teams
        if (isOdd)
        {
            teams.Add(null);
            n++;
        }

        var rounds = n - 1;
        var matchesPerRound = n / 2;

        for (var round = 0; round < rounds; round++)
        {
            var roundMatchups = new List<Matchup>();
            for (var match = 0; match < matchesPerRound; match++)
            {
                var home = (round + match) % (n - 1);
                var away = (n - 1 - match + round) % (n - 1);

                // Last team stays in place
                if (match == 0)
                {
                    away = n - 1;
                }

                var homeTeam = teams[home];
                var awayTeam = teams[away];

                if (homeTeam != null && awayTeam != null)
                {
                    roundMatchups.Add(new Matchup(round + 1, homeTeam.ParticipantId, awayTeam.ParticipantId));
                }
            }
            results.Add(new RoundMatchups(round + 1, roundMatchups));
        }

        // If home and away, add reverse fixtures
        if (homeAndAway)
        {
            var reverseRounds = new List<RoundMatchups>();
            for (var round = 0; round < rounds; round++)
            {
                var roundMatchups = new List<Matchup>();
                for (var match = 0; match < matchesPerRound; match++)
                {
                    var home = (round + match) % (n - 1);
                    var away = (n - 1 - match + round) % (n - 1);

                    if (match == 0)
                    {
                        away = n - 1;
                    }

                    var homeTeam = teams[home];
                    var awayTeam = teams[away];

                    if (homeTeam != null && awayTeam != null)
                    {
                        // Swap home and away for reverse fixture
                        roundMatchups.Add(new Matchup(rounds + round + 1, awayTeam.ParticipantId, homeTeam.ParticipantId));
                    }
                }
                reverseRounds.Add(new RoundMatchups(rounds + round + 1, roundMatchups));
            }
            results.AddRange(reverseRounds);
        }

        return results;
    }

    private Contest CreateContest(Stage stage, Guid sportId, int round, int matchNumber,
        Guid? homeId, Guid? awayId, string? homeType, string? awayType)
    {
        var contest = new Contest(
            Guid.NewGuid(),
            stage.TenantId,
            sportId,
            "RoundRobin");

        contest.AssignToStage(stage.Id, matchNumber);
        contest.AddParticipant(homeId ?? Guid.Empty, homeType ?? "Team", 1);
        contest.AddParticipant(awayId ?? Guid.Empty, awayType ?? "Team", 2);

        return contest;
    }

    // A deliberately small, stable PRNG keeps a stored seed reproducible across .NET versions.
    private static void Shuffle(List<ParticipantInfo> participants, int seed)
    {
        var state = unchecked((uint)seed);
        if (state == 0) state = 0x9E3779B9u;
        for (var index = participants.Count - 1; index > 0; index--)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            var swapIndex = (int)(state % (uint)(index + 1));
            (participants[index], participants[swapIndex]) =
                (participants[swapIndex], participants[index]);
        }
    }

    private record Matchup(int Round, Guid HomeId, Guid AwayId);
    private record RoundMatchups(int Round, List<Matchup> Matchups);
}
