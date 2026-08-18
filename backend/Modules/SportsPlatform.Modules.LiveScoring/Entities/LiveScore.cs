using SportsPlatform.BuildingBlocks;
using System.ComponentModel.DataAnnotations.Schema;

namespace SportsPlatform.Modules.LiveScoring.Entities;

public class LiveScore : AggregateRoot<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid ContestId { get; private set; }
    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public Guid ParticipantId { get; private set; }
    public Guid? SecondaryParticipantId { get; private set; }
    public Guid? PrimaryPlayerId { get; private set; }
    public Guid? SecondaryPlayerId { get; private set; }
    public Guid? SegmentId { get; private set; }
    public decimal? ClockSeconds { get; private set; }
    public string MetricName { get; private set; } = string.Empty;
    public decimal Value { get; private set; }
    public string? DisplayValue { get; private set; }
    public int Sequence { get; private set; }
    public long TimelineSequence { get; private set; }
    public string? ScorerId { get; private set; }
    public string? AssistId { get; private set; }
    public string? Metadata { get; private set; }
    public string DeviceId { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid? RulesetVersionId { get; private set; }
    public DateTime DeviceTimestamp { get; private set; }
    public DateTime ServerTimestamp { get; private set; }
    public DateTime ScoredAt { get; private set; }
    public new DateTime CreatedAt { get; private set; }

    private LiveScore() { }

    public LiveScore(
        Guid id,
        Guid contestId,
        Guid eventId,
        Guid participantId,
        string metricName,
        decimal value,
        Guid? segmentId = null,
        decimal? clockSeconds = null,
        Guid tenantId = default,
        string? eventType = null,
        Guid? secondaryParticipantId = null,
        DateTime? deviceTimestamp = null,
        string? deviceId = null,
        string? idempotencyKey = null,
        Guid? rulesetVersionId = null) : base(id)
    {
        TenantId = tenantId;
        ContestId = contestId;
        EventId = eventId;
        EventType = string.IsNullOrWhiteSpace(eventType) ? metricName : eventType.Trim();
        ParticipantId = participantId;
        SecondaryParticipantId = secondaryParticipantId;
        SegmentId = segmentId;
        ClockSeconds = clockSeconds;
        MetricName = metricName;
        Value = value;
        DeviceTimestamp = deviceTimestamp?.ToUniversalTime() ?? DateTime.UtcNow;
        ServerTimestamp = DateTime.UtcNow;
        DeviceId = string.IsNullOrWhiteSpace(deviceId) ? "server" : deviceId.Trim();
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
            ? eventId.ToString("N")
            : idempotencyKey.Trim();
        RulesetVersionId = rulesetVersionId;
        ScoredAt = DeviceTimestamp;
        CreatedAt = ServerTimestamp;
    }

    public void SetDisplayValue(string? displayValue)
    {
        DisplayValue = displayValue;
    }

    public void UpdateValue(decimal value, string? displayValue = null)
    {
        Value = value;
        DisplayValue = displayValue ?? value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public void Replace(
        string eventType,
        Guid participantId,
        Guid? secondaryParticipantId,
        Guid? segmentId,
        string metricName,
        decimal value,
        decimal? clockSeconds)
    {
        EventType = eventType.Trim();
        ParticipantId = participantId;
        SecondaryParticipantId = secondaryParticipantId;
        SegmentId = segmentId;
        MetricName = metricName.Trim();
        ClockSeconds = clockSeconds;
        UpdateValue(value);
    }

    public void SetSequence(int sequence)
    {
        Sequence = sequence;
    }

    public void AssignTimelineSequence(long sequence)
    {
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (TimelineSequence > 0 && TimelineSequence != sequence)
            throw new InvalidOperationException("A timeline sequence cannot be reassigned.");
        TimelineSequence = sequence;
    }

    public void SetScorer(string? scorerId, string? assistId)
    {
        ScorerId = scorerId;
        AssistId = assistId;
    }

    public void SetPlayers(Guid? primaryPlayerId, Guid? secondaryPlayerId)
    {
        if (primaryPlayerId.HasValue && primaryPlayerId == secondaryPlayerId)
            throw new ArgumentException("Primary and secondary players must be different.");
        PrimaryPlayerId = primaryPlayerId;
        SecondaryPlayerId = secondaryPlayerId;
    }

    public void SetMetadata(string? metadata)
    {
        Metadata = metadata;
    }

    public void SetContext(Guid? segmentId, decimal? clockSeconds)
    {
        SegmentId = segmentId;
        ClockSeconds = clockSeconds;
    }
}

public class LiveScoreUpdate : Entity<Guid>
{
    public Guid LiveScoreId { get; private set; }
    public string UpdateType { get; private set; } = string.Empty;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public string? UpdatedBy { get; private set; }
    public new DateTime UpdatedAt { get; private set; }

    private LiveScoreUpdate() { }

    public LiveScoreUpdate(Guid id, Guid liveScoreId, string updateType, string? oldValue, string? newValue, string? updatedBy) : base(id)
    {
        LiveScoreId = liveScoreId;
        UpdateType = updateType;
        OldValue = oldValue;
        NewValue = newValue;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}

public class LiveScoreSession : Entity<Guid>
{
    public Guid ContestId { get; private set; }
    public Guid EventId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public SessionStatus Status { get; private set; } = SessionStatus.Pending;
    public DateTime? StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public int TotalUpdates { get; private set; }
    public Guid? ActiveSegmentId { get; private set; }
    public decimal ClockSeconds { get; private set; }
    public bool ClockCountsDown { get; private set; }
    public bool IsClockRunning { get; private set; }
    public DateTime? ClockStartedAt { get; private set; }
    [NotMapped]
    public decimal CurrentClockSeconds => CalculateClock(DateTime.UtcNow);
    public new DateTime CreatedAt { get; private set; }

    private LiveScoreSession() { }

    public LiveScoreSession(Guid id, Guid contestId, Guid eventId, string name) : base(id)
    {
        ContestId = contestId;
        EventId = eventId;
        Name = name;
        CreatedAt = DateTime.UtcNow;
    }

    public void Start()
    {
        Status = SessionStatus.Active;
        StartedAt = DateTime.UtcNow;
    }

    public void Pause()
    {
        PauseClock();
        Status = SessionStatus.Paused;
    }

    public void Resume()
    {
        Status = SessionStatus.Active;
    }

    public void End()
    {
        PauseClock();
        Status = SessionStatus.Ended;
        EndedAt = DateTime.UtcNow;
    }

    public void IncrementUpdates()
    {
        TotalUpdates++;
    }

    public void SetContext(
        Guid activeSegmentId,
        decimal clockSeconds,
        bool clockCountsDown)
    {
        ClockSeconds = Math.Max(0, clockSeconds);
        ClockCountsDown = clockCountsDown;
        ActiveSegmentId = activeSegmentId;
        ClockStartedAt = IsClockRunning ? DateTime.UtcNow : null;
    }

    public void StartClock()
    {
        if (IsClockRunning) return;
        IsClockRunning = true;
        ClockStartedAt = DateTime.UtcNow;
    }

    public void PauseClock()
    {
        if (!IsClockRunning) return;
        ClockSeconds = CalculateClock(DateTime.UtcNow);
        IsClockRunning = false;
        ClockStartedAt = null;
    }

    private decimal CalculateClock(DateTime now)
    {
        if (!IsClockRunning || ClockStartedAt is null) return ClockSeconds;
        var elapsed = (decimal)(now - ClockStartedAt.Value).TotalSeconds;
        return ClockCountsDown
            ? Math.Max(0, ClockSeconds - elapsed)
            : ClockSeconds + elapsed;
    }
}

public enum SessionStatus
{
    Pending = 0,
    Active = 1,
    Paused = 2,
    Ended = 3,
    Cancelled = 4
}
