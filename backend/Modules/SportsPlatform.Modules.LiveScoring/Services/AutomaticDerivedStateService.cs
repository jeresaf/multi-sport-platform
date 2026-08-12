using SportsPlatform.Modules.Contests;
using SportsPlatform.Modules.Contests.Entities;

namespace SportsPlatform.Modules.LiveScoring.Services;

/// <summary>
/// Service for automatically calculating derived state from contest events.
/// This includes scores, standings, statistics, and other computed values.
/// </summary>
public class AutomaticDerivedStateService
{
    private readonly IContestRepository _contestRepository;

    public AutomaticDerivedStateService(IContestRepository contestRepository)
    {
        _contestRepository = contestRepository;
    }

    /// <summary>
    /// Calculates the complete derived state for a contest.
    /// </summary>
    public async Task<ContestDerivedState> CalculateDerivedStateAsync(Guid contestId)
    {
        var contest = await _contestRepository.GetByIdAsync(contestId);
        if (contest == null)
        {
            throw new InvalidOperationException($"Contest {contestId} not found.");
        }

        return CalculateDerivedState(contest);
    }

    /// <summary>
    /// Calculates the complete derived state for a contest.
    /// </summary>
    public ContestDerivedState CalculateDerivedState(Contest contest)
    {
        var approvedEvents = contest.Events
            .Where(e => e.Status == EventStatus.Approved)
            .OrderBy(e => e.SequenceNumber)
            .ToList();

        var participantScores = CalculateParticipantScores(contest, approvedEvents);
        var segmentStates = CalculateSegmentStates(contest, approvedEvents);
        var statistics = CalculateStatistics(contest, approvedEvents);
        var leaderboard = CalculateLeaderboard(contest, participantScores);
        var currentSegment = DetermineCurrentSegment(contest, approvedEvents);
        var matchProgress = CalculateMatchProgress(contest, approvedEvents);

        return new ContestDerivedState
        {
            ContestId = contest.Id,
            CalculatedAt = DateTime.UtcNow,
            ParticipantScores = participantScores,
            SegmentStates = segmentStates,
            Statistics = statistics,
            Leaderboard = leaderboard,
            CurrentSegmentId = currentSegment,
            MatchProgress = matchProgress,
            TotalEvents = approvedEvents.Count,
            LastEventTime = approvedEvents.LastOrDefault()?.ServerTimestamp
        };
    }

    /// <summary>
    /// Calculates scores for each participant based on scoring events.
    /// </summary>
    private Dictionary<Guid, ParticipantScore> CalculateParticipantScores(
        Contest contest, 
        List<ContestEvent> approvedEvents)
    {
        var scores = new Dictionary<Guid, ParticipantScore>();

        // Initialize scores for all participants
        foreach (var participant in contest.Participants)
        {
            scores[participant.ParticipantId] = new ParticipantScore
            {
                ParticipantId = participant.ParticipantId,
                ParticipantType = participant.ParticipantType,
                PrimaryScore = 0,
                SecondaryScore = 0,
                TertiaryScore = 0,
                ScoreBreakdown = new Dictionary<string, int>(),
                RecentEvents = new List<ScoreEvent>()
            };
        }

        // Process scoring events
        foreach (var evt in approvedEvents)
        {
            if (!scores.TryGetValue(evt.ParticipantId, out var score)) continue;

            var (primary, secondary, tertiary) = ParseScoreValue(evt.MetricValue);
            score.PrimaryScore += primary;
            score.SecondaryScore += secondary;
            score.TertiaryScore += tertiary;

            // Track breakdown by event type
            if (!score.ScoreBreakdown.ContainsKey(evt.EventType))
                score.ScoreBreakdown[evt.EventType] = 0;
            score.ScoreBreakdown[evt.EventType] += primary;

            // Track recent events for form calculation
            score.RecentEvents.Add(new ScoreEvent
            {
                EventType = evt.EventType,
                Value = primary,
                Timestamp = evt.ServerTimestamp,
                SegmentId = evt.SegmentId
            });

            // Keep only last 10 events for form
            if (score.RecentEvents.Count > 10)
                score.RecentEvents.RemoveAt(0);
        }

        return scores;
    }

    /// <summary>
    /// Calculates the state of each segment in the contest.
    /// </summary>
    private Dictionary<Guid, SegmentState> CalculateSegmentStates(
        Contest contest, 
        List<ContestEvent> approvedEvents)
    {
        var segmentStates = new Dictionary<Guid, SegmentState>();

        foreach (var segment in contest.Segments)
        {
            var segmentEvents = approvedEvents
                .Where(e => e.SegmentId == segment.Id)
                .ToList();

            var state = new SegmentState
            {
                SegmentId = segment.Id,
                SegmentName = segment.Name,
                SegmentType = segment.SegmentType,
                Status = segment.Status,
                EventCount = segmentEvents.Count,
                ParticipantScores = new Dictionary<Guid, int>(),
                StartTime = segment.StartedAt,
                EndTime = segment.EndedAt,
                Duration = segment.EndedAt.HasValue && segment.StartedAt.HasValue
                    ? segment.EndedAt.Value - segment.StartedAt.Value
                    : null
            };

            // Calculate scores within this segment
            foreach (var participant in contest.Participants)
            {
                var participantSegmentScore = segmentEvents
                    .Where(e => e.ParticipantId == participant.ParticipantId)
                    .Sum(e =>
                    {
                        var (primary, _, _) = ParseScoreValue(e.MetricValue);
                        return primary;
                    });
                state.ParticipantScores[participant.ParticipantId] = participantSegmentScore;
            }

            segmentStates[segment.Id] = state;
        }

        return segmentStates;
    }

    /// <summary>
    /// Calculates statistics for the contest.
    /// </summary>
    private ContestStatistics CalculateStatistics(
        Contest contest, 
        List<ContestEvent> approvedEvents)
    {
        var stats = new ContestStatistics
        {
            TotalEvents = approvedEvents.Count,
            EventsByType = new Dictionary<string, int>(),
            EventsByParticipant = new Dictionary<Guid, int>(),
            AverageEventsPerSegment = 0,
            FirstEventTime = approvedEvents.FirstOrDefault()?.ServerTimestamp,
            LastEventTime = approvedEvents.LastOrDefault()?.ServerTimestamp
        };

        // Count events by type
        foreach (var evt in approvedEvents)
        {
            if (!stats.EventsByType.ContainsKey(evt.EventType))
                stats.EventsByType[evt.EventType] = 0;
            stats.EventsByType[evt.EventType]++;

            if (!stats.EventsByParticipant.ContainsKey(evt.ParticipantId))
                stats.EventsByParticipant[evt.ParticipantId] = 0;
            stats.EventsByParticipant[evt.ParticipantId]++;
        }

        // Calculate average events per segment
        if (contest.Segments.Any())
        {
            stats.AverageEventsPerSegment = (double)approvedEvents.Count / contest.Segments.Count;
        }

        // Calculate event frequency
        if (stats.FirstEventTime.HasValue && stats.LastEventTime.HasValue)
        {
            var duration = stats.LastEventTime.Value - stats.FirstEventTime.Value;
            if (duration.TotalMinutes > 0)
            {
                stats.EventsPerMinute = approvedEvents.Count / duration.TotalMinutes;
            }
        }

        return stats;
    }

    /// <summary>
    /// Calculates the leaderboard based on participant scores.
    /// </summary>
    private List<LeaderboardEntry> CalculateLeaderboard(
        Contest contest, 
        Dictionary<Guid, ParticipantScore> participantScores)
    {
        var entries = participantScores.Values
            .Select(ps => new LeaderboardEntry
            {
                Rank = 0,
                ParticipantId = ps.ParticipantId,
                ParticipantType = ps.ParticipantType,
                PrimaryScore = ps.PrimaryScore,
                SecondaryScore = ps.SecondaryScore,
                TertiaryScore = ps.TertiaryScore,
                PositionChange = 0,
                Form = CalculateForm(ps.RecentEvents)
            })
            .OrderByDescending(e => e.PrimaryScore)
            .ThenByDescending(e => e.SecondaryScore)
            .ThenByDescending(e => e.TertiaryScore)
            .ToList();

        // Assign ranks
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].Rank = i + 1;
        }

        return entries;
    }

    /// <summary>
    /// Determines the current active segment based on events.
    /// </summary>
    private Guid? DetermineCurrentSegment(Contest contest, List<ContestEvent> approvedEvents)
    {
        // Find the most recent segment with events
        var lastEvent = approvedEvents.LastOrDefault();
        if (lastEvent?.SegmentId != null)
        {
            return lastEvent.SegmentId;
        }

        // If no events, return the first pending segment
        var pendingSegment = contest.Segments
            .FirstOrDefault(s => s.Status == SegmentStatus.Pending);
        
        return pendingSegment?.Id ?? contest.Segments.FirstOrDefault()?.Id;
    }

    /// <summary>
    /// Calculates match progress information.
    /// </summary>
    private MatchProgress CalculateMatchProgress(
        Contest contest, 
        List<ContestEvent> approvedEvents)
    {
        var progress = new MatchProgress
        {
            ContestStatus = contest.Status,
            TotalSegments = contest.Segments.Count,
            CompletedSegments = contest.Segments.Count(s => s.Status == SegmentStatus.Completed),
            CurrentSegmentIndex = 0,
            ElapsedTime = contest.StartTime.HasValue
                ? DateTime.UtcNow - contest.StartTime.Value
                : null,
            EstimatedRemainingTime = null
        };

        // Calculate current segment index
        var currentSegmentId = DetermineCurrentSegment(contest, approvedEvents);
        if (currentSegmentId.HasValue)
        {
            var currentIndex = contest.Segments
                .OrderBy(s => s.SortOrder)
                .ToList()
                .FindIndex(s => s.Id == currentSegmentId);
            progress.CurrentSegmentIndex = currentIndex >= 0 ? currentIndex : 0;
        }

        // Estimate remaining time based on average segment duration
        var completedSegments = contest.Segments
            .Where(s => s.EndedAt.HasValue && s.StartedAt.HasValue)
            .ToList();
        
        if (completedSegments.Any())
        {
            var avgSegmentDuration = TimeSpan.FromTicks(
                (long)completedSegments.Average(s => 
                    (s.EndedAt!.Value - s.StartedAt!.Value).Ticks));
            
            var remainingSegments = progress.TotalSegments - progress.CompletedSegments;
            progress.EstimatedRemainingTime = avgSegmentDuration * remainingSegments;
        }

        return progress;
    }

    /// <summary>
    /// Parses a score value string into primary, secondary, and tertiary components.
    /// </summary>
    private static (int primary, int secondary, int tertiary) ParseScoreValue(string? metricValue)
    {
        if (string.IsNullOrEmpty(metricValue))
            return (1, 0, 0);

        // Handle formats like "2", "2-1", "2-1-0"
        var parts = metricValue.Split('-');
        
        return parts.Length switch
        {
            1 => (int.TryParse(parts[0], out var p1) ? p1 : 1, 0, 0),
            2 => (int.TryParse(parts[0], out var p2) ? p2 : 0, 
                  int.TryParse(parts[1], out var s2) ? s2 : 0, 0),
            _ => (int.TryParse(parts[0], out var p3) ? p3 : 0, 
                  int.TryParse(parts[1], out var s3) ? s3 : 0,
                  int.TryParse(parts[2], out var t3) ? t3 : 0)
        };
    }

    /// <summary>
    /// Calculates form based on recent events.
    /// </summary>
    private static string CalculateForm(List<ScoreEvent> recentEvents)
    {
        if (!recentEvents.Any()) return "";

        // Simple form calculation based on last 5 events
        var last5 = recentEvents.TakeLast(5).ToList();
        return string.Join(",", last5.Select(e => e.Value > 0 ? "W" : "L"));
    }
}

/// <summary>
/// Represents the complete derived state of a contest.
/// </summary>
public class ContestDerivedState
{
    public Guid ContestId { get; set; }
    public DateTime CalculatedAt { get; set; }
    public Dictionary<Guid, ParticipantScore> ParticipantScores { get; set; } = new();
    public Dictionary<Guid, SegmentState> SegmentStates { get; set; } = new();
    public ContestStatistics Statistics { get; set; } = new();
    public List<LeaderboardEntry> Leaderboard { get; set; } = new();
    public Guid? CurrentSegmentId { get; set; }
    public MatchProgress MatchProgress { get; set; } = new();
    public int TotalEvents { get; set; }
    public DateTime? LastEventTime { get; set; }
}

/// <summary>
/// Score information for a participant.
/// </summary>
public class ParticipantScore
{
    public Guid ParticipantId { get; set; }
    public string ParticipantType { get; set; } = string.Empty;
    public int PrimaryScore { get; set; }
    public int SecondaryScore { get; set; }
    public int TertiaryScore { get; set; }
    public Dictionary<string, int> ScoreBreakdown { get; set; } = new();
    public List<ScoreEvent> RecentEvents { get; set; } = new();
}

/// <summary>
/// A score event for tracking recent activity.
/// </summary>
public class ScoreEvent
{
    public string EventType { get; set; } = string.Empty;
    public int Value { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? SegmentId { get; set; }
}

/// <summary>
/// State information for a segment.
/// </summary>
public class SegmentState
{
    public Guid SegmentId { get; set; }
    public string SegmentName { get; set; } = string.Empty;
    public string SegmentType { get; set; } = string.Empty;
    public SegmentStatus Status { get; set; }
    public int EventCount { get; set; }
    public Dictionary<Guid, int> ParticipantScores { get; set; } = new();
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// Statistics for a contest.
/// </summary>
public class ContestStatistics
{
    public int TotalEvents { get; set; }
    public Dictionary<string, int> EventsByType { get; set; } = new();
    public Dictionary<Guid, int> EventsByParticipant { get; set; } = new();
    public double AverageEventsPerSegment { get; set; }
    public double EventsPerMinute { get; set; }
    public DateTime? FirstEventTime { get; set; }
    public DateTime? LastEventTime { get; set; }
}

/// <summary>
/// A leaderboard entry.
/// </summary>
public class LeaderboardEntry
{
    public int Rank { get; set; }
    public Guid ParticipantId { get; set; }
    public string ParticipantType { get; set; } = string.Empty;
    public int PrimaryScore { get; set; }
    public int SecondaryScore { get; set; }
    public int TertiaryScore { get; set; }
    public int PositionChange { get; set; }
    public string Form { get; set; } = string.Empty;
}

/// <summary>
/// Progress information for a match.
/// </summary>
public class MatchProgress
{
    public ContestStatus ContestStatus { get; set; }
    public int TotalSegments { get; set; }
    public int CompletedSegments { get; set; }
    public int CurrentSegmentIndex { get; set; }
    public TimeSpan? ElapsedTime { get; set; }
    public TimeSpan? EstimatedRemainingTime { get; set; }
}