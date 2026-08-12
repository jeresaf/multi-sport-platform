using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;

namespace SportsPlatform.Api.Controllers;

internal static class CricketTournamentProjection
{
    public static async Task<object> BuildAsync(
        SportsPlatformDbContext database,
        Competition competition,
        CancellationToken cancellationToken)
    {
        var stages = competition.Editions.SelectMany(item => item.Stages)
            .OrderBy(item => item.SortOrder)
            .ToArray();
        var stageIds = stages.Select(item => item.Id).ToArray();
        var matches = await database.Contests
            .AsNoTracking()
            .Include(item => item.Participants)
            .Include(item => item.Events.Where(contestEvent =>
                contestEvent.EventType == "CricketInningsSummary"))
            .Where(item => item.StageId.HasValue
                && stageIds.Contains(item.StageId.Value)
                && item.SportId == CricketController.CricketSportId)
            .OrderBy(item => item.SortOrder)
            .ToArrayAsync(cancellationToken);
        var participantIds = matches.SelectMany(item => item.Participants)
            .Select(item => item.ParticipantId)
            .Distinct()
            .ToArray();
        var names = await database.Participants
            .AsNoTracking()
            .Where(item => participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var groupStageIds = stages.Where(item => item.Type == StageType.Group)
            .Select(item => item.Id)
            .ToHashSet();
        var groupMatches = matches
            .Where(item => groupStageIds.Contains(item.StageId!.Value))
            .ToArray();
        var standings = CalculateStandings(groupMatches)
            .Select((item, index) => new
            {
                position = index + 1,
                item.ParticipantId,
                name = names.GetValueOrDefault(item.ParticipantId),
                item.Played,
                item.Won,
                item.Lost,
                item.Tied,
                item.Points,
                item.RunsFor,
                item.RunsAgainst,
                item.NetRunRate
            });

        object ProjectMatch(Contest match)
        {
            var innings = CricketController.ReadInnings(match);
            return new
            {
                match.Id,
                match.Name,
                match.ContestShape,
                status = match.Status.ToString(),
                teams = match.Participants.OrderBy(item => item.SortOrder)
                    .Select(item => new
                    {
                        item.ParticipantId,
                        name = names.GetValueOrDefault(item.ParticipantId)
                    }),
                innings = innings.Values.OrderBy(item => item.InningsNumber)
                    .Select(item => new
                    {
                        item.InningsNumber,
                        item.BattingTeamId,
                        name = names.GetValueOrDefault(item.BattingTeamId),
                        item.Runs,
                        item.Wickets,
                        item.Balls,
                        overs = CricketController.FormatOvers(item.Balls),
                        item.Extras
                    }),
                target = innings.TryGetValue(1, out var first) ? first.Runs + 1 : (int?)null,
                match.WinnerId,
                winnerName = match.WinnerId.HasValue
                    ? names.GetValueOrDefault(match.WinnerId.Value)
                    : null,
                result = ResultDescription(match, innings)
            };
        }

        var group = groupMatches.Select(ProjectMatch);
        var semifinals = matches.Where(item => item.ContestShape == "CricketSemifinal")
            .Select(ProjectMatch);
        var finalMatch = matches.SingleOrDefault(item =>
            item.ContestShape == "CricketFinal");
        return new
        {
            competition.Id,
            competition.Name,
            standings,
            groupMatches = group,
            semifinals,
            final = finalMatch is null ? null : ProjectMatch(finalMatch),
            championId = finalMatch?.WinnerId,
            championName = finalMatch?.WinnerId is Guid winnerId
                ? names.GetValueOrDefault(winnerId)
                : null
        };
    }

    internal static IReadOnlyList<CricketStandingRow> CalculateStandings(
        IReadOnlyCollection<Contest> matches)
    {
        var participantIds = matches.SelectMany(item => item.Participants)
            .Select(item => item.ParticipantId)
            .Distinct();
        var rows = participantIds.ToDictionary(
            item => item,
            item => new MutableStanding(item));
        foreach (var match in matches.Where(item =>
                     item.Status == ContestStatus.Completed))
        {
            var innings = CricketController.ReadInnings(match);
            if (innings.Count != 2) continue;
            var first = innings[1];
            var second = innings[2];
            rows[first.BattingTeamId].Played++;
            rows[second.BattingTeamId].Played++;
            rows[first.BattingTeamId].RunsFor += first.Runs;
            rows[first.BattingTeamId].RunsAgainst += second.Runs;
            rows[first.BattingTeamId].BallsFaced += first.Balls;
            rows[first.BattingTeamId].BallsBowled += second.Balls;
            rows[second.BattingTeamId].RunsFor += second.Runs;
            rows[second.BattingTeamId].RunsAgainst += first.Runs;
            rows[second.BattingTeamId].BallsFaced += second.Balls;
            rows[second.BattingTeamId].BallsBowled += first.Balls;
            if (match.WinnerId is Guid winnerId)
            {
                var loserId = match.Participants
                    .Single(item => item.ParticipantId != winnerId)
                    .ParticipantId;
                rows[winnerId].Won++;
                rows[winnerId].Points += 2;
                rows[loserId].Lost++;
            }
            else
            {
                rows[first.BattingTeamId].Tied++;
                rows[first.BattingTeamId].Points++;
                rows[second.BattingTeamId].Tied++;
                rows[second.BattingTeamId].Points++;
            }
        }

        return rows.Values.Select(item => item.ToRow())
            .OrderByDescending(item => item.Points)
            .ThenByDescending(item => item.NetRunRate)
            .ThenBy(item => item.ParticipantId)
            .ToArray();
    }

    internal static string? ResultDescription(
        Contest match,
        IReadOnlyDictionary<int, CricketInningsSummary> innings)
    {
        if (innings.Count != 2) return null;
        var first = innings[1];
        var second = innings[2];
        if (!match.WinnerId.HasValue) return "Tied — Super Over required";
        return match.WinnerId == second.BattingTeamId
            ? $"Won by {10 - second.Wickets} wickets"
            : $"Won by {first.Runs - second.Runs} runs";
    }

    private sealed class MutableStanding
    {
        public MutableStanding(Guid participantId) => ParticipantId = participantId;
        public Guid ParticipantId { get; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Lost { get; set; }
        public int Tied { get; set; }
        public int Points { get; set; }
        public int RunsFor { get; set; }
        public int RunsAgainst { get; set; }
        public int BallsFaced { get; set; }
        public int BallsBowled { get; set; }

        public CricketStandingRow ToRow()
        {
            var runRateFor = BallsFaced == 0 ? 0 : RunsFor / (BallsFaced / 6m);
            var runRateAgainst = BallsBowled == 0 ? 0 : RunsAgainst / (BallsBowled / 6m);
            return new CricketStandingRow(
                ParticipantId,
                Played,
                Won,
                Lost,
                Tied,
                Points,
                RunsFor,
                RunsAgainst,
                decimal.Round(runRateFor - runRateAgainst, 3));
        }
    }
}

internal sealed record CricketStandingRow(
    Guid ParticipantId,
    int Played,
    int Won,
    int Lost,
    int Tied,
    int Points,
    int RunsFor,
    int RunsAgainst,
    decimal NetRunRate);
