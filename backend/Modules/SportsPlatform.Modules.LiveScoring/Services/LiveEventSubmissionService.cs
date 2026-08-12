using SportsPlatform.Modules.Contests;
using SportsPlatform.Modules.Contests.Entities;

namespace SportsPlatform.Modules.LiveScoring.Services;

/// <summary>
/// Service for submitting live events during a contest with validation.
/// </summary>
public class LiveEventSubmissionService
{
    private readonly IContestRepository _contestRepository;

    public LiveEventSubmissionService(IContestRepository contestRepository)
    {
        _contestRepository = contestRepository;
    }

    /// <summary>
    /// Validates and submits a live event to a contest.
    /// </summary>
    public async Task<EventSubmissionResult> SubmitEventAsync(
        Guid contestId,
        Guid tenantId,
        string eventType,
        Guid participantId,
        Guid? secondaryParticipantId,
        Guid? segmentId,
        string? metricValue,
        Guid scorerId,
        string deviceId,
        long sequenceNumber,
        string idempotencyKey)
    {
        // Validate contest exists and is in progress
        var contest = await _contestRepository.GetByIdAsync(contestId);
        if (contest == null)
        {
            return EventSubmissionResult.Failure("Contest not found.");
        }

        if (contest.TenantId != tenantId)
        {
            return EventSubmissionResult.Failure("Contest does not belong to this tenant.");
        }

        // Validate contest status
        var statusValidation = ValidateContestStatus(contest);
        if (!statusValidation.IsValid)
        {
            return EventSubmissionResult.Failure(statusValidation.Message!);
        }

        // Validate event type
        var eventValidation = ValidateEventType(eventType);
        if (!eventValidation.IsValid)
        {
            return EventSubmissionResult.Failure(eventValidation.Message!);
        }

        // Validate participant exists in contest
        var participantValidation = ValidateParticipant(contest, participantId);
        if (!participantValidation.IsValid)
        {
            return EventSubmissionResult.Failure(participantValidation.Message!);
        }

        // Validate segment if provided
        if (segmentId.HasValue)
        {
            var segmentValidation = ValidateSegment(contest, segmentId.Value);
            if (!segmentValidation.IsValid)
            {
                return EventSubmissionResult.Failure(segmentValidation.Message!);
            }
        }

        // Validate idempotency - prevent duplicate submissions
        var existingEvent = contest.Events
            .FirstOrDefault(e => e.IdempotencyKey == idempotencyKey);
        if (existingEvent != null)
        {
            return EventSubmissionResult.AlreadyProcessed(existingEvent.Id);
        }

        // Validate sequence number is greater than last event
        var lastEvent = contest.Events.OrderByDescending(e => e.SequenceNumber).FirstOrDefault();
        if (lastEvent != null && sequenceNumber <= lastEvent.SequenceNumber)
        {
            return EventSubmissionResult.Failure(
                $"Sequence number must be greater than {lastEvent.SequenceNumber}. Got: {sequenceNumber}");
        }

        // Create the event
        var contestEvent = contest.AddEvent(
            eventType,
            participantId,
            segmentId,
            metricValue,
            scorerId,
            deviceId,
            sequenceNumber,
            idempotencyKey);

        _contestRepository.Update(contest);
        await _contestRepository.SaveChangesAsync();

        return EventSubmissionResult.CreateSuccess(contestEvent.Id);
    }

    /// <summary>
    /// Validates that a contest can accept events.
    /// </summary>
    private static (bool IsValid, string? Message) ValidateContestStatus(Contest contest)
    {
        return contest.Status switch
        {
            ContestStatus.InProgress => (true, null),
            ContestStatus.Paused => (true, null), // Events can still be submitted when paused
            _ => (false, $"Contest is not in progress. Current status: {contest.Status}")
        };
    }

    /// <summary>
    /// Validates the event type is recognized.
    /// </summary>
    private static (bool IsValid, string? Message) ValidateEventType(string eventType)
    {
        var validEventTypes = new[]
        {
            "Goal", "Point", "Score", "Foul", "Card", "Substitution",
            "Penalty", "Corner", "FreeKick", "ThrowIn", "YellowCard", "RedCard",
            "Start", "End", "Timeout", "Injury", "Review", "Custom"
        };

        if (!validEventTypes.Contains(eventType, StringComparer.OrdinalIgnoreCase))
        {
            return (false, $"Unknown event type: {eventType}");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates the participant is registered and eligible.
    /// </summary>
    private static (bool IsValid, string? Message) ValidateParticipant(Contest contest, Guid participantId)
    {
        var participant = contest.Participants.FirstOrDefault(p => p.ParticipantId == participantId);
        
        if (participant == null)
        {
            return (false, "Participant not found in contest.");
        }

        if (participant.Status == ContestParticipantStatus.Withdrawn)
        {
            return (false, "Participant has withdrawn from contest.");
        }

        if (participant.Status == ContestParticipantStatus.Disqualified)
        {
            return (false, "Participant is disqualified and cannot have events recorded.");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates the segment exists in the contest.
    /// </summary>
    private static (bool IsValid, string? Message) ValidateSegment(Contest contest, Guid segmentId)
    {
        var segment = contest.Segments.FirstOrDefault(s => s.Id == segmentId);
        
        if (segment == null)
        {
            return (false, "Segment not found in contest.");
        }

        return (true, null);
    }
}

/// <summary>
/// Result of an event submission attempt.
/// </summary>
public class EventSubmissionResult
{
    public bool IsSuccess { get; private init; }
    public Guid? EventId { get; private init; }
    public string? ErrorMessage { get; private init; }
    public bool IsAlreadyProcessed { get; private init; }

    private EventSubmissionResult() { }

    public static EventSubmissionResult CreateSuccess(Guid eventId) => new()
    {
        IsSuccess = true,
        EventId = eventId
    };

    public static EventSubmissionResult Failure(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };

    public static EventSubmissionResult AlreadyProcessed(Guid eventId) => new()
    {
        IsSuccess = true,
        EventId = eventId,
        IsAlreadyProcessed = true
    };
}