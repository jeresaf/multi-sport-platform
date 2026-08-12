using SportsPlatform.Modules.Scheduling.Entities;

namespace SportsPlatform.Modules.Scheduling.Services;

public sealed record CompetitionScheduleEvent(
    Guid ContestId,
    string Name,
    string? Venue,
    DateTime? StartTime,
    int DurationMinutes,
    IReadOnlyCollection<Guid> ParticipantIds,
    IReadOnlyCollection<Guid> OfficialIds);

public sealed record ScheduleConflict(
    string Key,
    string Type,
    string Message,
    Guid ContestId,
    Guid? ConflictingContestId = null);

public sealed class CompetitionScheduleConflictDetector
{
    public IReadOnlyList<ScheduleConflict> Detect(
        IEnumerable<CompetitionScheduleEvent> source,
        CompetitionSchedulePolicy policy)
    {
        var events = source.OrderBy(item => item.StartTime).ThenBy(item => item.ContestId).ToArray();
        var conflicts = new List<ScheduleConflict>();

        foreach (var item in events)
        {
            if (!item.StartTime.HasValue)
            {
                Add(conflicts, "Unscheduled", item, null, $"'{item.Name}' has not been scheduled.");
                continue;
            }

            var start = item.StartTime.Value;
            var end = start.AddMinutes(item.DurationMinutes);
            var startMinutes = start.Hour * 60 + start.Minute;
            var endMinutes = end.Date > start.Date ? 1440 : end.Hour * 60 + end.Minute;

            if (policy.GetBlackoutDates().Contains(DateOnly.FromDateTime(start)))
                Add(conflicts, "BlackoutDate", item, null,
                    $"'{item.Name}' is scheduled on blackout date {start:yyyy-MM-dd}.");
            if (startMinutes < policy.WindowStartMinutes || endMinutes > policy.WindowEndMinutes)
                Add(conflicts, "TimeWindow", item, null,
                    $"'{item.Name}' falls outside the permitted daily time window.");
        }

        for (var leftIndex = 0; leftIndex < events.Length; leftIndex++)
        {
            var left = events[leftIndex];
            if (!left.StartTime.HasValue) continue;
            var leftEnd = left.StartTime.Value.AddMinutes(left.DurationMinutes);

            for (var rightIndex = leftIndex + 1; rightIndex < events.Length; rightIndex++)
            {
                var right = events[rightIndex];
                if (!right.StartTime.HasValue) continue;
                var rightEnd = right.StartTime.Value.AddMinutes(right.DurationMinutes);
                if (right.StartTime.Value >= leftEnd.AddMinutes(Math.Max(
                        policy.MinimumRestMinutes,
                        policy.VenueTurnaroundMinutes)))
                    break;

                var overlaps = left.StartTime.Value < rightEnd
                    && right.StartTime.Value < leftEnd;
                var sameVenue = !string.IsNullOrWhiteSpace(left.Venue)
                    && string.Equals(left.Venue.Trim(), right.Venue?.Trim(), StringComparison.OrdinalIgnoreCase);
                var sharedParticipants = left.ParticipantIds.Intersect(right.ParticipantIds).Any();
                var sharedOfficials = left.OfficialIds.Intersect(right.OfficialIds).Any();

                if (sameVenue && right.StartTime.Value < leftEnd.AddMinutes(policy.VenueTurnaroundMinutes))
                    Add(conflicts, "VenueConflict", left, right,
                        $"Venue '{left.Venue}' is unavailable between '{left.Name}' and '{right.Name}'.");
                if (sharedParticipants && overlaps)
                    Add(conflicts, "ParticipantConflict", left, right,
                        $"A participant is assigned to overlapping contests '{left.Name}' and '{right.Name}'.");
                if (sharedParticipants && right.StartTime.Value < leftEnd.AddMinutes(policy.MinimumRestMinutes))
                    Add(conflicts, "MinimumRest", left, right,
                        $"A participant does not receive the required {policy.MinimumRestMinutes} minutes of rest.");
                if (sharedOfficials && overlaps)
                    Add(conflicts, "OfficialConflict", left, right,
                        $"An official is assigned to overlapping contests '{left.Name}' and '{right.Name}'.");
            }
        }

        return conflicts.OrderBy(item => item.Type).ThenBy(item => item.Key).ToArray();
    }

    private static void Add(
        ICollection<ScheduleConflict> conflicts,
        string type,
        CompetitionScheduleEvent contest,
        CompetitionScheduleEvent? other,
        string message)
    {
        var ids = new[] { contest.ContestId, other?.ContestId ?? Guid.Empty }.Order().ToArray();
        conflicts.Add(new ScheduleConflict(
            $"{type}:{ids[0]:N}:{ids[1]:N}",
            type,
            message,
            contest.ContestId,
            other?.ContestId));
    }
}
