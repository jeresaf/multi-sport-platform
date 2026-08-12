using System.Text.Json;
using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Scheduling.Entities;

public sealed class CompetitionSchedulePolicy : TenantAggregateRoot<Guid>
{
    public Guid CompetitionId { get; private set; }
    public int DefaultDurationMinutes { get; private set; } = 1;
    public int MinimumRestMinutes { get; private set; }
    public int VenueTurnaroundMinutes { get; private set; }
    public int WindowStartMinutes { get; private set; } = 0;
    public int WindowEndMinutes { get; private set; } = 1440;
    public string BlackoutDatesJson { get; private set; } = "[]";
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private CompetitionSchedulePolicy() { }

    public CompetitionSchedulePolicy(Guid id, Guid competitionId, Guid tenantId)
        : base(id, tenantId)
    {
        CompetitionId = competitionId;
        CreatedAt = DateTime.UtcNow;
    }

    public void Configure(
        int defaultDurationMinutes,
        int minimumRestMinutes,
        int venueTurnaroundMinutes,
        int windowStartMinutes,
        int windowEndMinutes,
        IEnumerable<DateOnly>? blackoutDates)
    {
        if (defaultDurationMinutes is < 1 or > 1440)
            throw new ArgumentOutOfRangeException(nameof(defaultDurationMinutes));
        if (minimumRestMinutes is < 0 or > 10080)
            throw new ArgumentOutOfRangeException(nameof(minimumRestMinutes));
        if (venueTurnaroundMinutes is < 0 or > 1440)
            throw new ArgumentOutOfRangeException(nameof(venueTurnaroundMinutes));
        if (windowStartMinutes is < 0 or > 1439)
            throw new ArgumentOutOfRangeException(nameof(windowStartMinutes));
        if (windowEndMinutes is < 1 or > 1440 || windowEndMinutes <= windowStartMinutes)
            throw new ArgumentOutOfRangeException(nameof(windowEndMinutes));

        DefaultDurationMinutes = defaultDurationMinutes;
        MinimumRestMinutes = minimumRestMinutes;
        VenueTurnaroundMinutes = venueTurnaroundMinutes;
        WindowStartMinutes = windowStartMinutes;
        WindowEndMinutes = windowEndMinutes;
        BlackoutDatesJson = JsonSerializer.Serialize(
            (blackoutDates ?? Array.Empty<DateOnly>()).Distinct().Order().ToArray());
        UpdatedAt = DateTime.UtcNow;
    }

    public IReadOnlySet<DateOnly> GetBlackoutDates() =>
        (JsonSerializer.Deserialize<DateOnly[]>(BlackoutDatesJson) ?? Array.Empty<DateOnly>())
        .ToHashSet();
}

public sealed class ScheduleConflictOverride : TenantEntity<Guid>
{
    public Guid CompetitionId { get; private set; }
    public string ConflictKey { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public Guid RequestedBy { get; private set; }
    public new DateTime CreatedAt { get; private set; }

    private ScheduleConflictOverride() { }

    public ScheduleConflictOverride(
        Guid id,
        Guid competitionId,
        Guid tenantId,
        string conflictKey,
        string reason,
        Guid requestedBy) : base(id, tenantId)
    {
        if (string.IsNullOrWhiteSpace(conflictKey))
            throw new ArgumentException("A conflict key is required.", nameof(conflictKey));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An override reason is required.", nameof(reason));

        CompetitionId = competitionId;
        ConflictKey = conflictKey.Trim();
        Reason = reason.Trim();
        RequestedBy = requestedBy;
        CreatedAt = DateTime.UtcNow;
    }

    public void Revise(string reason, Guid requestedBy)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An override reason is required.", nameof(reason));
        Reason = reason.Trim();
        RequestedBy = requestedBy;
        CreatedAt = DateTime.UtcNow;
    }
}

public sealed class FixtureAdjustment : TenantEntity<Guid>
{
    public Guid CompetitionId { get; private set; }
    public Guid ContestId { get; private set; }
    public string Venue { get; private set; } = string.Empty;
    public DateTime StartTime { get; private set; }
    public int DurationMinutes { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid RequestedBy { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public FixtureAdjustmentStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? ReviewNotes { get; private set; }

    private FixtureAdjustment() { }

    public FixtureAdjustment(
        Guid id,
        Guid competitionId,
        Guid contestId,
        Guid tenantId,
        string venue,
        DateTime startTime,
        int durationMinutes,
        string reason,
        Guid requestedBy) : base(id, tenantId)
    {
        if (competitionId == Guid.Empty) throw new ArgumentException("A competition is required.", nameof(competitionId));
        if (contestId == Guid.Empty) throw new ArgumentException("A contest is required.", nameof(contestId));
        if (string.IsNullOrWhiteSpace(venue)) throw new ArgumentException("A venue is required.", nameof(venue));
        if (durationMinutes is < 1 or > 1440) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("An adjustment reason is required.", nameof(reason));

        CompetitionId = competitionId;
        ContestId = contestId;
        Venue = venue.Trim();
        StartTime = startTime.Kind == DateTimeKind.Utc ? startTime : startTime.ToUniversalTime();
        DurationMinutes = durationMinutes;
        Reason = reason.Trim();
        RequestedBy = requestedBy;
        RequestedAt = DateTime.UtcNow;
        Status = FixtureAdjustmentStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void Approve(Guid reviewedBy, string? notes)
    {
        EnsurePending();
        if (reviewedBy == RequestedBy)
            throw new InvalidOperationException("A fixture adjustment requires approval by a different user.");
        Status = FixtureAdjustmentStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
        ReviewNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(Guid reviewedBy, string? notes)
    {
        EnsurePending();
        if (reviewedBy == RequestedBy)
            throw new InvalidOperationException("A fixture adjustment requires review by a different user.");
        Status = FixtureAdjustmentStatus.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
        ReviewNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsurePending()
    {
        if (Status != FixtureAdjustmentStatus.Pending)
            throw new InvalidOperationException("Only pending fixture adjustments can be reviewed.");
    }
}

public enum FixtureAdjustmentStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
