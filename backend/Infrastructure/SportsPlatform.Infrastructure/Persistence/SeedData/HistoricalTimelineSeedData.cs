using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.LiveScoring.Entities;
using SportsPlatform.Modules.Results.Entities;

namespace SportsPlatform.Infrastructure.Persistence.SeedData;

/// <summary>
/// Ensures every seeded public result has a minimal, truthful historical
/// timeline even when the source fixture supplied only aggregate results.
/// </summary>
public static class HistoricalTimelineSeedData
{
    public static async Task EnsureAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken = default)
    {
        var resultContestIds = await context.ContestResults
            .Where(item => item.Status == ContestResultStatus.Published
                || item.Status == ContestResultStatus.Official)
            .Select(item => item.ContestId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (resultContestIds.Length == 0)
            return;

        var timelineContestIds = await context.LiveScores
            .Where(item => resultContestIds.Contains(item.ContestId))
            .Select(item => item.ContestId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var missing = resultContestIds.Except(timelineContestIds).ToArray();
        if (missing.Length == 0)
            return;

        var contests = await context.Contests
            .Where(item => missing.Contains(item.Id))
            .ToArrayAsync(cancellationToken);
        var results = await context.ContestResults
            .Where(item => missing.Contains(item.ContestId)
                && (item.Status == ContestResultStatus.Published
                    || item.Status == ContestResultStatus.Official))
            .OrderBy(item => item.Rank ?? int.MaxValue)
            .ThenBy(item => item.ParticipantId)
            .ToArrayAsync(cancellationToken);

        foreach (var contest in contests)
        {
            var contestResults = results
                .Where(item => item.ContestId == contest.Id)
                .ToArray();
            if (contestResults.Length == 0)
                continue;

            var occurredAt = contest.StartTime ?? contestResults
                .Min(item => item.ApprovedAt ?? item.CreatedAt);
            var carrierId = contestResults[0].ParticipantId;
            var sequence = 1;
            Add("MATCH_START", carrierId, "event", 0, "Start", occurredAt, 0);
            foreach (var result in contestResults)
            {
                Add(
                    "SCORE_ADJUSTMENT",
                    result.ParticipantId,
                    "score",
                    result.Score ?? 0,
                    result.ScoreDisplay ?? result.Score?.ToString() ?? result.Position ?? "Result",
                    occurredAt.AddMinutes(119),
                    7140);
            }
            Add("MATCH_END", carrierId, "event", 0, "Full time",
                occurredAt.AddMinutes(120), 7200);

            void Add(
                string eventType,
                Guid participantId,
                string metric,
                decimal value,
                string display,
                DateTime timestamp,
                decimal clockSeconds)
            {
                var key = $"historical:{contest.Id}:{sequence}:{eventType}:{participantId}";
                var score = new LiveScore(
                    StableId(key),
                    contest.Id,
                    StableId($"event:{key}"),
                    participantId,
                    metric,
                    value,
                    clockSeconds: clockSeconds,
                    tenantId: contest.TenantId,
                    eventType: eventType,
                    deviceTimestamp: timestamp,
                    deviceId: "historical-timeline-backfill",
                    idempotencyKey: key,
                    rulesetVersionId: contest.RulesetVersionId);
                score.SetDisplayValue(display);
                score.SetSequence(sequence++);
                score.SetMetadata(JsonSerializer.Serialize(new
                {
                    description = "Historical aggregate result"
                }));
                context.LiveScores.Add(score);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Guid StableId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }
}
