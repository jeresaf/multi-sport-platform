using SportsPlatform.Modules.Scheduling.Entities;

namespace SportsPlatform.Modules.Scheduling.Services;

public class SchedulingConstraintsService
{
    public SchedulingConstraintsResult ValidateConstraints(
        List<ScheduledEvent> events,
        SchedulingConstraints constraints)
    {
        var result = new SchedulingConstraintsResult();

        // Validate venue availability
        ValidateVenueAvailability(events, constraints, result);

        // Validate rest days
        ValidateRestDays(events, constraints, result);

        // Validate travel constraints
        ValidateTravelConstraints(events, constraints, result);

        // Validate time windows
        ValidateTimeWindows(events, constraints, result);

        // Validate conflict constraints
        ValidateConflicts(events, constraints, result);

        return result;
    }

    private void ValidateVenueAvailability(
        List<ScheduledEvent> events,
        SchedulingConstraints constraints,
        SchedulingConstraintsResult result)
    {
        var venueSchedules = events
            .Where(e => !string.IsNullOrEmpty(e.Location))
            .GroupBy(e => e.Location)
            .ToDictionary(g => g.Key!, g => g.ToList());

        foreach (var (venue, venueEvents) in venueSchedules)
        {
            var sortedEvents = venueEvents.OrderBy(e => e.ScheduledStart).ToList();
            
            for (int i = 0; i < sortedEvents.Count - 1; i++)
            {
                var current = sortedEvents[i];
                var next = sortedEvents[i + 1];
                
                var gapMinutes = (next.ScheduledStart - current.ScheduledEnd).TotalMinutes;
                
                if (gapMinutes < constraints.MinVenueTurnaroundMinutes)
                {
                    result.Violations.Add(new ConstraintViolation
                    {
                        Type = ConstraintViolationType.VenueTurnaround,
                        Message = $"Venue '{venue}' has insufficient turnaround time ({gapMinutes:F0} min) between events",
                        Event1Id = current.Id,
                        Event2Id = next.Id,
                        Severity = ViolationSeverity.Warning
                    });
                }

                // Check for venue double-booking
                if (current.ScheduledStart < next.ScheduledEnd && 
                    current.ScheduledEnd > next.ScheduledStart)
                {
                    result.Violations.Add(new ConstraintViolation
                    {
                        Type = ConstraintViolationType.VenueConflict,
                        Message = $"Venue '{venue}' is double-booked: '{current.Name}' and '{next.Name}' overlap",
                        Event1Id = current.Id,
                        Event2Id = next.Id,
                        Severity = ViolationSeverity.Error
                    });
                }
            }

            // Check venue operating hours
            if (constraints.VenueOperatingHours != null && 
                constraints.VenueOperatingHours.TryGetValue(venue, out var hours))
            {
                foreach (var evt in venueEvents)
                {
                    var startHour = evt.ScheduledStart.Hour;
                    var endHour = evt.ScheduledEnd.Hour;
                    
                    if (startHour < hours.StartHour || endHour > hours.EndHour)
                    {
                        result.Violations.Add(new ConstraintViolation
                        {
                            Type = ConstraintViolationType.VenueOperatingHours,
                            Message = $"Event '{evt.Name}' at venue '{venue}' is outside operating hours ({hours.StartHour}:00-{hours.EndHour}:00)",
                            EventId = evt.Id,
                            Severity = ViolationSeverity.Warning
                        });
                    }
                }
            }
        }
    }

    private void ValidateRestDays(
        List<ScheduledEvent> events,
        SchedulingConstraints constraints,
        SchedulingConstraintsResult result)
    {
        if (constraints.MinRestDaysBetweenEvents == 0)
            return;

        // Group events by participant/team
        var participantEvents = events
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .GroupBy(e => e.Name)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.ScheduledStart).ToList());

        foreach (var (participant, participantEventList) in participantEvents)
        {
            for (int i = 0; i < participantEventList.Count - 1; i++)
            {
                var current = participantEventList[i];
                var next = participantEventList[i + 1];
                
                var daysBetween = (next.ScheduledStart.Date - current.ScheduledEnd.Date).Days;
                
                if (daysBetween < constraints.MinRestDaysBetweenEvents)
                {
                    result.Violations.Add(new ConstraintViolation
                    {
                        Type = ConstraintViolationType.InsufficientRestDays,
                        Message = $"Participant '{participant}' has only {daysBetween} rest days (minimum: {constraints.MinRestDaysBetweenEvents})",
                        Event1Id = current.Id,
                        Event2Id = next.Id,
                        Severity = ViolationSeverity.Warning
                    });
                }
            }
        }
    }

    private void ValidateTravelConstraints(
        List<ScheduledEvent> events,
        SchedulingConstraints constraints,
        SchedulingConstraintsResult result)
    {
        if (!constraints.EnableTravelConstraints)
            return;

        var locationEvents = events
            .Where(e => !string.IsNullOrEmpty(e.Location))
            .GroupBy(e => e.Location)
            .ToDictionary(g => g.Key!, g => g.OrderBy(e => e.ScheduledStart).ToList());

        foreach (var (location, locationEventList) in locationEvents)
        {
            for (int i = 0; i < locationEventList.Count - 1; i++)
            {
                var current = locationEventList[i];
                var next = locationEventList[i + 1];
                
                var gapHours = (next.ScheduledStart - current.ScheduledEnd).TotalHours;
                
                // Check for back-to-back events at different locations
                if (gapHours < constraints.MinTravelHoursBetweenLocations && current.Location != next.Location)
                {
                    result.Violations.Add(new ConstraintViolation
                    {
                        Type = ConstraintViolationType.TravelTime,
                        Message = $"Insufficient travel time ({gapHours:F1}h) from '{current.Location}' to '{next.Location}'",
                        Event1Id = current.Id,
                        Event2Id = next.Id,
                        Severity = ViolationSeverity.Warning
                    });
                }
            }
        }
    }

    private void ValidateTimeWindows(
        List<ScheduledEvent> events,
        SchedulingConstraints constraints,
        SchedulingConstraintsResult result)
    {
        foreach (var evt in events)
        {
            // Check if event is scheduled during blackout dates
            if (constraints.BlackoutDates != null)
            {
                var eventDate = evt.ScheduledStart.Date;
                if (constraints.BlackoutDates.Contains(eventDate))
                {
                    result.Violations.Add(new ConstraintViolation
                    {
                        Type = ConstraintViolationType.BlackoutDate,
                        Message = $"Event '{evt.Name}' is scheduled on blackout date {eventDate:yyyy-MM-dd}",
                        EventId = evt.Id,
                        Severity = ViolationSeverity.Error
                    });
                }
            }

            // Check preferred time windows
            if (constraints.PreferredTimeWindows != null)
            {
                var hour = evt.ScheduledStart.Hour;
                var isInPreferredWindow = constraints.PreferredTimeWindows.Any(
                    w => hour >= w.StartHour && hour < w.EndHour);

                if (!isInPreferredWindow)
                {
                    result.Violations.Add(new ConstraintViolation
                    {
                        Type = ConstraintViolationType.OutsidePreferredWindow,
                        Message = $"Event '{evt.Name}' is outside preferred time windows",
                        EventId = evt.Id,
                        Severity = ViolationSeverity.Info
                    });
                }
            }
        }
    }

    private void ValidateConflicts(
        List<ScheduledEvent> events,
        SchedulingConstraints constraints,
        SchedulingConstraintsResult result)
    {
        // Check for same-time events for same participant
        var eventPairs = events
            .SelectMany((e1, i) => events.Skip(i + 1), (e1, e2) => new { e1, e2 })
            .Where(pair => pair.e1.ScheduledStart == pair.e2.ScheduledStart)
            .ToList();

        foreach (var pair in eventPairs)
        {
            result.Violations.Add(new ConstraintViolation
            {
                Type = ConstraintViolationType.SimultaneousEvents,
                Message = $"Events '{pair.e1.Name}' and '{pair.e2.Name}' are scheduled at the same time",
                Event1Id = pair.e1.Id,
                Event2Id = pair.e2.Id,
                Severity = ViolationSeverity.Error
            });
        }
    }

    public List<ScheduledEvent> OptimizeSchedule(
        List<ScheduledEvent> events,
        SchedulingConstraints constraints)
    {
        var result = events.ToList();
        var iterations = 0;
        var maxIterations = 100;

        while (iterations < maxIterations)
        {
            var violationsBefore = result.Count(v => 
                result.Any(other => other != v && 
                    v.ScheduledStart == other.ScheduledStart && 
                    v.Location == other.Location));

            // Try to resolve venue conflicts by shifting events
            result = ResolveVenueConflicts(result, constraints);
            
            // Try to add rest days by reordering
            result = OptimizeRestDays(result, constraints);

            var violationsAfter = result.Count(v => 
                result.Any(other => other != v && 
                    v.ScheduledStart == other.ScheduledStart && 
                    v.Location == other.Location));

            if (violationsBefore == violationsAfter)
                break;

            iterations++;
        }

        return result;
    }

    private List<ScheduledEvent> ResolveVenueConflicts(
        List<ScheduledEvent> events,
        SchedulingConstraints constraints)
    {
        var result = events.ToList();
        var venueGroups = result
            .Where(e => !string.IsNullOrEmpty(e.Location))
            .GroupBy(e => e.Location!)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.ScheduledStart).ToList());

        foreach (var (venue, venueEvents) in venueGroups)
        {
            for (int i = 0; i < venueEvents.Count - 1; i++)
            {
                var current = venueEvents[i];
                var next = venueEvents[i + 1];

                if (current.ScheduledEnd > next.ScheduledStart)
                {
                    // Shift next event to after current event
                    var newStart = current.ScheduledEnd.AddMinutes(constraints.MinVenueTurnaroundMinutes);
                    var duration = next.Duration;
                    
                    next.Postpone(newStart, duration);
                }
            }
        }

        return result;
    }

    private List<ScheduledEvent> OptimizeRestDays(
        List<ScheduledEvent> events,
        SchedulingConstraints constraints)
    {
        if (constraints.MinRestDaysBetweenEvents == 0)
            return events;

        var result = events.OrderBy(e => e.ScheduledStart).ToList();
        
        for (int i = 0; i < result.Count - 1; i++)
        {
            var current = result[i];
            var next = result[i + 1];
            
            var daysBetween = (next.ScheduledStart.Date - current.ScheduledEnd.Date).Days;
            
            if (daysBetween < constraints.MinRestDaysBetweenEvents)
            {
                var requiredDays = constraints.MinRestDaysBetweenEvents - daysBetween;
                var newStart = next.ScheduledStart.AddDays(requiredDays);
                
                next.Postpone(newStart, next.Duration);
            }
        }

        return result;
    }
}

public class SchedulingConstraints
{
    public int MinVenueTurnaroundMinutes { get; set; } = 30;
    public int MinRestDaysBetweenEvents { get; set; } = 1;
    public bool EnableTravelConstraints { get; set; } = true;
    public double MinTravelHoursBetweenLocations { get; set; } = 2;
    public Dictionary<string, VenueOperatingHours>? VenueOperatingHours { get; set; }
    public List<DateTime>? BlackoutDates { get; set; }
    public List<TimeWindow>? PreferredTimeWindows { get; set; }
}

public class VenueOperatingHours
{
    public int StartHour { get; set; }
    public int EndHour { get; set; }
}

public class TimeWindow
{
    public int StartHour { get; set; }
    public int EndHour { get; set; }
}

public class SchedulingConstraintsResult
{
    public bool IsValid => Violations.All(v => v.Severity != ViolationSeverity.Error);
    public List<ConstraintViolation> Violations { get; set; } = new();
    public int ErrorCount => Violations.Count(v => v.Severity == ViolationSeverity.Error);
    public int WarningCount => Violations.Count(v => v.Severity == ViolationSeverity.Warning);
}

public class ConstraintViolation
{
    public ConstraintViolationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? EventId { get; set; }
    public Guid? Event1Id { get; set; }
    public Guid? Event2Id { get; set; }
    public ViolationSeverity Severity { get; set; }
}

public enum ConstraintViolationType
{
    VenueTurnaround,
    VenueConflict,
    VenueOperatingHours,
    InsufficientRestDays,
    TravelTime,
    BlackoutDate,
    OutsidePreferredWindow,
    SimultaneousEvents
}

public enum ViolationSeverity
{
    Info,
    Warning,
    Error
}