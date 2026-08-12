using SportsPlatform.Modules.Contests;
using SportsPlatform.Modules.Contests.Entities;

namespace SportsPlatform.Modules.LiveScoring.Services;

/// <summary>
/// Service for managing event corrections and reversals with approval workflow.
/// </summary>
public class CorrectionWorkflowService
{
    private readonly IContestRepository _contestRepository;

    public CorrectionWorkflowService(IContestRepository contestRepository)
    {
        _contestRepository = contestRepository;
    }

    /// <summary>
    /// Submits a correction request for an event.
    /// </summary>
    public async Task<CorrectionResult> SubmitCorrectionAsync(
        Guid contestId,
        Guid tenantId,
        Guid eventId,
        string correctionReason,
        string newEventType,
        Guid? newParticipantId,
        string? newMetricValue,
        Guid submittedBy)
    {
        var contest = await _contestRepository.GetByIdAsync(contestId);
        if (contest == null)
        {
            return CorrectionResult.Failure("Contest not found.");
        }

        if (contest.TenantId != tenantId)
        {
            return CorrectionResult.Failure("Contest does not belong to this tenant.");
        }

        var originalEvent = contest.Events.FirstOrDefault(e => e.Id == eventId);
        if (originalEvent == null)
        {
            return CorrectionResult.Failure("Event not found.");
        }

        // Validate the correction reason
        if (string.IsNullOrWhiteSpace(correctionReason))
        {
            return CorrectionResult.Failure("Correction reason is required.");
        }

        // Create a new event that supersedes the original
        var correctionEvent = contest.AddEvent(
            newEventType,
            newParticipantId ?? originalEvent.ParticipantId,
            originalEvent.SegmentId,
            newMetricValue ?? originalEvent.MetricValue,
            submittedBy,
            $"CORRECTION-{originalEvent.DeviceId}",
            originalEvent.SequenceNumber + 1, // Slightly after original
            $"CORRECTION-{eventId}-{Guid.NewGuid()}");

        // Mark the original event as superseded
        originalEvent.Correct(correctionReason, correctionEvent.Id);

        _contestRepository.Update(contest);
        await _contestRepository.SaveChangesAsync();

        return CorrectionResult.CorrectionSubmitted(correctionEvent.Id, originalEvent.Id);
    }

    /// <summary>
    /// Approves a correction event.
    /// </summary>
    public async Task<CorrectionResult> ApproveCorrectionAsync(
        Guid contestId,
        Guid tenantId,
        Guid correctionEventId,
        Guid approvedBy)
    {
        var contest = await _contestRepository.GetByIdAsync(contestId);
        if (contest == null)
        {
            return CorrectionResult.Failure("Contest not found.");
        }

        if (contest.TenantId != tenantId)
        {
            return CorrectionResult.Failure("Contest does not belong to this tenant.");
        }

        var correctionEvent = contest.Events.FirstOrDefault(e => e.Id == correctionEventId);
        if (correctionEvent == null)
        {
            return CorrectionResult.Failure("Correction event not found.");
        }

        if (correctionEvent.Status == EventStatus.Approved)
        {
            return CorrectionResult.Failure("Correction is already approved.");
        }

        correctionEvent.Approve();

        _contestRepository.Update(contest);
        await _contestRepository.SaveChangesAsync();

        return CorrectionResult.Approved(correctionEventId);
    }

    /// <summary>
    /// Rejects a correction event.
    /// </summary>
    public async Task<CorrectionResult> RejectCorrectionAsync(
        Guid contestId,
        Guid tenantId,
        Guid correctionEventId,
        string rejectionReason,
        Guid rejectedBy)
    {
        var contest = await _contestRepository.GetByIdAsync(contestId);
        if (contest == null)
        {
            return CorrectionResult.Failure("Contest not found.");
        }

        if (contest.TenantId != tenantId)
        {
            return CorrectionResult.Failure("Contest does not belong to this tenant.");
        }

        var correctionEvent = contest.Events.FirstOrDefault(e => e.Id == correctionEventId);
        if (correctionEvent == null)
        {
            return CorrectionResult.Failure("Correction event not found.");
        }

        if (correctionEvent.Status == EventStatus.Rejected)
        {
            return CorrectionResult.Failure("Correction is already rejected.");
        }

        correctionEvent.Reject(rejectionReason);

        // Restore the original event while retaining the rejected correction
        // as immutable audit history.
        var originalEvent = contest.Events.FirstOrDefault(
            item => item.SupersededByEventId == correctionEvent.Id);
        if (originalEvent is not null)
        {
            originalEvent.RestoreAfterRejectedCorrection();
        }

        _contestRepository.Update(contest);
        await _contestRepository.SaveChangesAsync();

        return CorrectionResult.Rejected(correctionEventId);
    }

    /// <summary>
    /// Gets all correction events for a contest.
    /// </summary>
    public async Task<IReadOnlyList<ContestEvent>> GetCorrectionEventsAsync(Guid contestId)
    {
        var contest = await _contestRepository.GetByIdAsync(contestId);
        if (contest == null)
        {
            return Array.Empty<ContestEvent>();
        }

        return contest.Events
            .Where(e => e.IdempotencyKey.StartsWith("CORRECTION-"))
            .OrderBy(e => e.ServerTimestamp)
            .ToList();
    }

    /// <summary>
    /// Gets pending corrections for approval.
    /// </summary>
    public async Task<IReadOnlyList<ContestEvent>> GetPendingCorrectionsAsync(Guid contestId)
    {
        var contest = await _contestRepository.GetByIdAsync(contestId);
        if (contest == null)
        {
            return Array.Empty<ContestEvent>();
        }

        return contest.Events
            .Where(e => e.IdempotencyKey.StartsWith("CORRECTION-") && e.Status == EventStatus.Pending)
            .OrderBy(e => e.ServerTimestamp)
            .ToList();
    }
}

/// <summary>
/// Result of a correction workflow operation.
/// </summary>
public class CorrectionResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorMessage { get; private init; }
    public Guid? CorrectionEventId { get; private init; }
    public Guid? OriginalEventId { get; private init; }
    public CorrectionResultType ResultType { get; private init; }

    private CorrectionResult() { }

    public static CorrectionResult Failure(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message,
        ResultType = CorrectionResultType.Failure
    };

    public static CorrectionResult CorrectionSubmitted(Guid correctionEventId, Guid originalEventId) => new()
    {
        IsSuccess = true,
        CorrectionEventId = correctionEventId,
        OriginalEventId = originalEventId,
        ResultType = CorrectionResultType.Submitted
    };

    public static CorrectionResult Approved(Guid correctionEventId) => new()
    {
        IsSuccess = true,
        CorrectionEventId = correctionEventId,
        ResultType = CorrectionResultType.Approved
    };

    public static CorrectionResult Rejected(Guid correctionEventId) => new()
    {
        IsSuccess = true,
        CorrectionEventId = correctionEventId,
        ResultType = CorrectionResultType.Rejected
    };
}

public enum CorrectionResultType
{
    Failure,
    Submitted,
    Approved,
    Rejected
}
