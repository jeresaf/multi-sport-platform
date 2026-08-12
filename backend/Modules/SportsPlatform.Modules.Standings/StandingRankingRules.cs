namespace SportsPlatform.Modules.Standings;

public enum StandingRankingProfile
{
    Football,
    Rugby
}

public sealed record StandingRankCandidate(
    Guid ParticipantId,
    decimal Points,
    int PointsFor,
    int PointsAgainst,
    int Tries = 0,
    int Wins = 0);

public sealed record RankedStanding(Guid ParticipantId, int Position);
public sealed record StandingHeadToHead(decimal Points, int PointsFor, int PointsAgainst);

public static class StandingRankingRules
{
    public static IReadOnlyList<RankedStanding> Rank(
        IEnumerable<StandingRankCandidate> candidates,
        IEnumerable<string> tieBreakers)
    {
        var rules = tieBreakers.Select(item => item.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();
        var comparer = Comparer<StandingRankCandidate>.Create((left, right) =>
        {
            foreach (var rule in rules)
            {
                var comparison = rule switch
                {
                    "points" => right.Points.CompareTo(left.Points),
                    "pointdifferential" =>
                        (right.PointsFor - right.PointsAgainst)
                            .CompareTo(left.PointsFor - left.PointsAgainst),
                    "pointsfor" => right.PointsFor.CompareTo(left.PointsFor),
                    "wins" => right.Wins.CompareTo(left.Wins),
                    _ => throw new ArgumentException($"Unsupported tie-breaker '{rule}'.")
                };
                if (comparison != 0) return comparison;
            }
            return left.ParticipantId.CompareTo(right.ParticipantId);
        });
        var ordered = candidates.OrderBy(item => item, comparer).ToArray();
        var result = new List<RankedStanding>(ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            var position = index + 1;
            if (index > 0 && ConfiguredCriteriaMatch(ordered[index - 1], ordered[index], rules))
                position = result[index - 1].Position;
            result.Add(new RankedStanding(ordered[index].ParticipantId, position));
        }
        return result;
    }

    public static IReadOnlyList<RankedStanding> Rank(
        IEnumerable<StandingRankCandidate> candidates,
        StandingRankingProfile profile,
        IReadOnlyDictionary<Guid, StandingHeadToHead>? headToHead = null)
    {
        headToHead ??= new Dictionary<Guid, StandingHeadToHead>();
        var ordered = candidates
            .OrderByDescending(candidate => candidate.Points)
            .ThenByDescending(candidate => candidate.PointsFor - candidate.PointsAgainst)
            .ThenByDescending(candidate => profile == StandingRankingProfile.Rugby
                ? candidate.Tries
                : candidate.PointsFor)
            .ThenByDescending(candidate => profile == StandingRankingProfile.Football
                ? headToHead.GetValueOrDefault(candidate.ParticipantId)?.Points ?? 0
                : 0)
            .ThenByDescending(candidate => profile == StandingRankingProfile.Football
                ? (headToHead.GetValueOrDefault(candidate.ParticipantId)?.PointsFor ?? 0)
                    - (headToHead.GetValueOrDefault(candidate.ParticipantId)?.PointsAgainst ?? 0)
                : 0)
            .ThenByDescending(candidate => profile == StandingRankingProfile.Football
                ? headToHead.GetValueOrDefault(candidate.ParticipantId)?.PointsFor ?? 0
                : 0)
            .ThenBy(candidate => candidate.ParticipantId)
            .ToArray();

        var result = new List<RankedStanding>(ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            var position = index + 1;
            if (index > 0 && SportingCriteriaMatch(
                    ordered[index - 1], ordered[index], profile, headToHead))
                position = result[index - 1].Position;
            result.Add(new RankedStanding(ordered[index].ParticipantId, position));
        }

        return result;
    }

    private static bool SportingCriteriaMatch(
        StandingRankCandidate left,
        StandingRankCandidate right,
        StandingRankingProfile profile,
        IReadOnlyDictionary<Guid, StandingHeadToHead> headToHead) =>
        left.Points == right.Points &&
        left.PointsFor - left.PointsAgainst == right.PointsFor - right.PointsAgainst &&
        (profile == StandingRankingProfile.Rugby
            ? left.Tries == right.Tries
            : left.PointsFor == right.PointsFor
                && HeadToHeadCriteriaMatch(left.ParticipantId, right.ParticipantId, headToHead));

    private static bool HeadToHeadCriteriaMatch(
        Guid left,
        Guid right,
        IReadOnlyDictionary<Guid, StandingHeadToHead> headToHead)
    {
        var leftValue = headToHead.GetValueOrDefault(left) ?? new StandingHeadToHead(0, 0, 0);
        var rightValue = headToHead.GetValueOrDefault(right) ?? new StandingHeadToHead(0, 0, 0);
        return leftValue.Points == rightValue.Points
            && leftValue.PointsFor - leftValue.PointsAgainst
                == rightValue.PointsFor - rightValue.PointsAgainst
            && leftValue.PointsFor == rightValue.PointsFor;
    }

    private static bool ConfiguredCriteriaMatch(
        StandingRankCandidate left,
        StandingRankCandidate right,
        IReadOnlyCollection<string> rules) => rules.All(rule => rule switch
        {
            "points" => left.Points == right.Points,
            "pointdifferential" => left.PointsFor - left.PointsAgainst
                == right.PointsFor - right.PointsAgainst,
            "pointsfor" => left.PointsFor == right.PointsFor,
            "wins" => left.Wins == right.Wins,
            _ => false
        });
}
