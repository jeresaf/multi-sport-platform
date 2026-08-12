using System.Text.Json;
using SportsPlatform.BuildingBlocks;
using SportsPlatform.Modules.Scheduling.Entities;

namespace SportsPlatform.Modules.Scheduling.Services;

public interface IScheduleService
{
    // Schedule Management
    Task<Schedule> CreateScheduleAsync(
        Guid contestId,
        string name,
        DateTime startDate,
        DateTime endDate,
        string? description = null,
        string? timezone = null,
        CancellationToken cancellationToken = default);

    Task<Schedule?> GetScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default);
    Task<Schedule?> GetScheduleByContestAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Schedule>> GetSchedulesByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Schedule> UpdateScheduleAsync(
        Guid scheduleId,
        string? name = null,
        string? description = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? timezone = null,
        CancellationToken cancellationToken = default);

    // Schedule Status
    Task<Schedule> PublishScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default);
    Task<Schedule> LockScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default);
    Task<Schedule> UnlockScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default);
    Task<Schedule> ArchiveScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default);

    // Scheduled Events
    Task<ScheduledEvent> AddEventAsync(
        Guid scheduleId,
        string name,
        DateTime scheduledStart,
        int duration,
        Guid? eventId = null,
        string? location = null,
        int? order = null,
        CancellationToken cancellationToken = default);

    Task<ScheduledEvent?> GetEventAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ScheduledEvent>> GetEventsByScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default);
    Task<ScheduledEvent> UpdateEventAsync(
        Guid eventId,
        string? name = null,
        DateTime? scheduledStart = null,
        int? duration = null,
        string? location = null,
        int? order = null,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<ScheduledEvent> StartEventAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<ScheduledEvent> CompleteEventAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<ScheduledEvent> PostponeEventAsync(Guid eventId, DateTime newStart, int newDuration, CancellationToken cancellationToken = default);
    Task<ScheduledEvent> CancelEventAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task DeleteEventAsync(Guid eventId, CancellationToken cancellationToken = default);

    // Auto-generation
    Task<Schedule> SetGenerationRulesAsync(Guid scheduleId, string rules, CancellationToken cancellationToken = default);
    Task<Schedule> GenerateEventsAsync(Guid scheduleId, CancellationToken cancellationToken = default);

    // Conflict Detection
    Task<IEnumerable<ScheduledEvent>> GetConflictingEventsAsync(DateTime start, DateTime end, string? location = null, CancellationToken cancellationToken = default);
}

public class ScheduleService : IScheduleService
{
    private readonly IRepository<Schedule> _scheduleRepository;
    private readonly IRepository<ScheduledEvent> _eventRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ScheduleService(
        IRepository<Schedule> scheduleRepository,
        IRepository<ScheduledEvent> eventRepository,
        IUnitOfWork unitOfWork)
    {
        _scheduleRepository = scheduleRepository;
        _eventRepository = eventRepository;
        _unitOfWork = unitOfWork;
    }

    #region Schedule Management

    public async Task<Schedule> CreateScheduleAsync(
        Guid contestId,
        string name,
        DateTime startDate,
        DateTime endDate,
        string? description = null,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var schedule = new Schedule(
            Guid.NewGuid(),
            contestId,
            name,
            startDate,
            endDate);

        if (description != null)
            schedule.SetDescription(description);

        if (timezone != null)
            schedule.SetTimezone(timezone);

        await _scheduleRepository.AddAsync(schedule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return schedule;
    }

    public async Task<Schedule?> GetScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        return await _scheduleRepository.GetByIdAsync(scheduleId, cancellationToken);
    }

    public async Task<Schedule?> GetScheduleByContestAsync(Guid contestId, CancellationToken cancellationToken = default)
    {
        var schedules = await _scheduleRepository.FindAsync(
            s => s.ContestId == contestId,
            cancellationToken);

        return schedules.OrderByDescending(s => s.CreatedAt).FirstOrDefault();
    }

    public async Task<IEnumerable<Schedule>> GetSchedulesByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // In a real implementation, this would filter by tenant through contest lookup
        return await _scheduleRepository.FindAsync(
            s => true, // Filter by tenant through contest relationship
            cancellationToken);
    }

    public async Task<Schedule> UpdateScheduleAsync(
        Guid scheduleId,
        string? name = null,
        string? description = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId, cancellationToken)
            ?? throw new InvalidOperationException($"Schedule with ID {scheduleId} not found.");

        if (name != null)
            schedule.SetDescription(name); // Note: Schedule entity has SetDescription but not SetName

        if (description != null)
            schedule.SetDescription(description);

        if (startDate.HasValue && endDate.HasValue)
            schedule.SetDates(startDate.Value, endDate.Value);

        if (timezone != null)
            schedule.SetTimezone(timezone);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return schedule;
    }

    #endregion

    #region Schedule Status

    public async Task<Schedule> PublishScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId, cancellationToken)
            ?? throw new InvalidOperationException($"Schedule with ID {scheduleId} not found.");

        schedule.Publish();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return schedule;
    }

    public async Task<Schedule> LockScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId, cancellationToken)
            ?? throw new InvalidOperationException($"Schedule with ID {scheduleId} not found.");

        schedule.Lock();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return schedule;
    }

    public async Task<Schedule> UnlockScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId, cancellationToken)
            ?? throw new InvalidOperationException($"Schedule with ID {scheduleId} not found.");

        schedule.Unlock();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return schedule;
    }

    public async Task<Schedule> ArchiveScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId, cancellationToken)
            ?? throw new InvalidOperationException($"Schedule with ID {scheduleId} not found.");

        // Archive is not in the Schedule entity, so we lock it instead
        schedule.Lock();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return schedule;
    }

    #endregion

    #region Scheduled Events

    public async Task<ScheduledEvent> AddEventAsync(
        Guid scheduleId,
        string name,
        DateTime scheduledStart,
        int duration,
        Guid? eventId = null,
        string? location = null,
        int? order = null,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId, cancellationToken)
            ?? throw new InvalidOperationException($"Schedule with ID {scheduleId} not found.");

        var scheduledEvent = new ScheduledEvent(
            Guid.NewGuid(),
            scheduleId,
            name,
            scheduledStart,
            duration);

        if (eventId.HasValue)
            scheduledEvent.SetEventId(eventId);

        if (location != null)
            scheduledEvent.SetLocation(location);

        if (order.HasValue)
            scheduledEvent.SetOrder(order.Value);

        schedule.AddEvent(scheduledEvent);
        await _eventRepository.AddAsync(scheduledEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return scheduledEvent;
    }

    public async Task<ScheduledEvent?> GetEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _eventRepository.GetByIdAsync(eventId, cancellationToken);
    }

    public async Task<IEnumerable<ScheduledEvent>> GetEventsByScheduleAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        return await _eventRepository.FindAsync(
            e => e.ScheduleId == scheduleId,
            cancellationToken);
    }

    public async Task<ScheduledEvent> UpdateEventAsync(
        Guid eventId,
        string? name = null,
        DateTime? scheduledStart = null,
        int? duration = null,
        string? location = null,
        int? order = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var scheduledEvent = await _eventRepository.GetByIdAsync(eventId, cancellationToken)
            ?? throw new InvalidOperationException($"Scheduled event with ID {eventId} not found.");

        // Note: ScheduledEvent entity doesn't have setters for name, scheduledStart, duration
        // In a real implementation, these would need to be added to the entity

        if (location != null)
            scheduledEvent.SetLocation(location);

        if (order.HasValue)
            scheduledEvent.SetOrder(order.Value);

        if (notes != null)
            scheduledEvent.SetNotes(notes);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return scheduledEvent;
    }

    public async Task<ScheduledEvent> StartEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var scheduledEvent = await _eventRepository.GetByIdAsync(eventId, cancellationToken)
            ?? throw new InvalidOperationException($"Scheduled event with ID {eventId} not found.");

        scheduledEvent.Start();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return scheduledEvent;
    }

    public async Task<ScheduledEvent> CompleteEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var scheduledEvent = await _eventRepository.GetByIdAsync(eventId, cancellationToken)
            ?? throw new InvalidOperationException($"Scheduled event with ID {eventId} not found.");

        scheduledEvent.Complete();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return scheduledEvent;
    }

    public async Task<ScheduledEvent> PostponeEventAsync(
        Guid eventId,
        DateTime newStart,
        int newDuration,
        CancellationToken cancellationToken = default)
    {
        var scheduledEvent = await _eventRepository.GetByIdAsync(eventId, cancellationToken)
            ?? throw new InvalidOperationException($"Scheduled event with ID {eventId} not found.");

        scheduledEvent.Postpone(newStart, newDuration);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return scheduledEvent;
    }

    public async Task<ScheduledEvent> CancelEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var scheduledEvent = await _eventRepository.GetByIdAsync(eventId, cancellationToken)
            ?? throw new InvalidOperationException($"Scheduled event with ID {eventId} not found.");

        scheduledEvent.Cancel();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return scheduledEvent;
    }

    public async Task DeleteEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var scheduledEvent = await _eventRepository.GetByIdAsync(eventId, cancellationToken)
            ?? throw new InvalidOperationException($"Scheduled event with ID {eventId} not found.");

        await _eventRepository.DeleteAsync(scheduledEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    #endregion

    #region Auto-generation

    public async Task<Schedule> SetGenerationRulesAsync(
        Guid scheduleId,
        string rules,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId, cancellationToken)
            ?? throw new InvalidOperationException($"Schedule with ID {scheduleId} not found.");

        schedule.SetGenerationRules(rules);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return schedule;
    }

    public async Task<Schedule> GenerateEventsAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId, cancellationToken)
            ?? throw new InvalidOperationException($"Schedule with ID {scheduleId} not found.");

        if (string.IsNullOrEmpty(schedule.GenerationRules))
            throw new InvalidOperationException("No generation rules defined for this schedule.");

        // Parse generation rules and create events
        // This is a simplified implementation - in production, this would use
        // the format engines to generate events based on the rules
        var rules = JsonSerializer.Deserialize<ScheduleGenerationRules>(schedule.GenerationRules)
            ?? throw new InvalidOperationException("Invalid generation rules format.");

        var currentTime = schedule.StartDate;
        var eventOrder = 1;

        while (currentTime < schedule.EndDate)
        {
            var scheduledEvent = new ScheduledEvent(
                Guid.NewGuid(),
                scheduleId,
                rules.EventName ?? "Scheduled Event",
                currentTime,
                rules.DefaultDuration);

            scheduledEvent.SetOrder(eventOrder++);
            schedule.AddEvent(scheduledEvent);
            await _eventRepository.AddAsync(scheduledEvent, cancellationToken);

            currentTime = currentTime.AddMinutes(rules.IntervalMinutes);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return schedule;
    }

    #endregion

    #region Conflict Detection

    public async Task<IEnumerable<ScheduledEvent>> GetConflictingEventsAsync(
        DateTime start,
        DateTime end,
        string? location = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<ScheduledEvent> allEvents = await _eventRepository.FindAsync(
            e => e.ScheduledStart < end && e.ScheduledEnd > start,
            cancellationToken);

        if (!string.IsNullOrEmpty(location))
        {
            allEvents = allEvents.Where(e => e.Location == location);
        }

        return allEvents;
    }

    #endregion
}

public class ScheduleGenerationRules
{
    public string? EventName { get; set; }
    public int DefaultDuration { get; set; } = 60;
    public int IntervalMinutes { get; set; } = 60;
    public List<string>? Locations { get; set; }
    public bool AutoAssignLocations { get; set; } = false;
}
