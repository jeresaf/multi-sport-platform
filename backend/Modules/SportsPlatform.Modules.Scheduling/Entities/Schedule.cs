using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Scheduling.Entities;

public class Schedule : AggregateRoot<Guid>
{
    public Guid ContestId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public string? Timezone { get; private set; }
    public ScheduleStatus Status { get; private set; } = ScheduleStatus.Draft;
    public bool AutoGenerate { get; private set; }
    public string? GenerationRules { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private readonly List<ScheduledEvent> _events = new();
    public IReadOnlyCollection<ScheduledEvent> Events => _events.AsReadOnly();

    private Schedule() { }

    public Schedule(Guid id, Guid contestId, string name, DateTime startDate, DateTime endDate) : base(id)
    {
        ContestId = contestId;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetDescription(string? description)
    {
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetTimezone(string? timezone)
    {
        Timezone = timezone;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDates(DateTime startDate, DateTime endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetGenerationRules(string? rules)
    {
        GenerationRules = rules;
        AutoGenerate = !string.IsNullOrEmpty(rules);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        if (Status != ScheduleStatus.Draft)
            throw new InvalidOperationException("Only draft schedules can be published.");
        
        Status = ScheduleStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Lock()
    {
        Status = ScheduleStatus.Locked;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unlock()
    {
        Status = ScheduleStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddEvent(ScheduledEvent scheduledEvent)
    {
        _events.Add(scheduledEvent);
        UpdatedAt = DateTime.UtcNow;
    }
}

public class ScheduledEvent : AggregateRoot<Guid>
{
    public Guid ScheduleId { get; private set; }
    public Guid? EventId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime ScheduledStart { get; private set; }
    public DateTime ScheduledEnd { get; private set; }
    public int Duration { get; private set; }
    public string? Location { get; private set; }
    public int Order { get; private set; }
    public ScheduledEventStatus Status { get; private set; } = ScheduledEventStatus.Scheduled;
    public string? Notes { get; private set; }
    public DateTime? ActualStart { get; private set; }
    public DateTime? ActualEnd { get; private set; }

    private ScheduledEvent() { }

    public ScheduledEvent(Guid id, Guid scheduleId, string name, DateTime scheduledStart, int duration) : base(id)
    {
        ScheduleId = scheduleId;
        Name = name;
        ScheduledStart = scheduledStart;
        ScheduledEnd = scheduledStart.AddMinutes(duration);
        Duration = duration;
    }

    public void SetEventId(Guid? eventId)
    {
        EventId = eventId;
    }

    public void SetLocation(string? location)
    {
        Location = location;
    }

    public void SetOrder(int order)
    {
        Order = order;
    }

    public void SetNotes(string? notes)
    {
        Notes = notes;
    }

    public void Start()
    {
        Status = ScheduledEventStatus.InProgress;
        ActualStart = DateTime.UtcNow;
    }

    public void Complete()
    {
        Status = ScheduledEventStatus.Completed;
        ActualEnd = DateTime.UtcNow;
    }

    public void Postpone(DateTime newStart, int newDuration)
    {
        Status = ScheduledEventStatus.Postponed;
        ScheduledStart = newStart;
        ScheduledEnd = newStart.AddMinutes(newDuration);
        Duration = newDuration;
    }

    public void Cancel()
    {
        Status = ScheduledEventStatus.Cancelled;
    }
}

public enum ScheduleStatus
{
    Draft = 0,
    Published = 1,
    Locked = 2,
    Archived = 3
}

public enum ScheduledEventStatus
{
    Scheduled = 0,
    InProgress = 1,
    Completed = 2,
    Postponed = 3,
    Cancelled = 4,
    Delayed = 5
}
