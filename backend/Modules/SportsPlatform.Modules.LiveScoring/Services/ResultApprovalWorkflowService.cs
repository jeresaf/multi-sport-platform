using SportsPlatform.Modules.Contests;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Results;
using SportsPlatform.Modules.Results.Entities;

namespace SportsPlatform.Modules.LiveScoring.Services;

/// <summary>
/// Service for managing result submission and approval workflow.
/// </summary>
public class ResultApprovalWorkflowService
{
    private readonly IContestRepository _contestRepository;
    private readonly IResultRepository _resultRepository;

    public ResultApprovalWorkflowService(
        IContestRepository contestRepository,
        IResultRepository resultRepository)
    {
        _contestRepository = contestRepository;
        _resultRepository = resultRepository;
    }

    /// <summary>
    /// Submits a contest result for approval.
    /// </summary>
    public async Task<ResultApprovalResult> SubmitForApprovalAsync(
        Guid contestId,
        Guid tenantId,
        Guid submittedBy)
    {
        var contest = await _contestRepository.GetByIdAsync(contestId);
        if (contest == null)
        {
            return ResultApprovalResult.Failure("Contest not found.");
        }

        if (contest.TenantId != tenantId)
        {
            return ResultApprovalResult.Failure("Contest does not belong to this tenant.");
        }

        // Validate contest is in a submittable state
        if (contest.Status != ContestStatus.Completed && contest.Status != ContestStatus.Corrected)
        {
            return ResultApprovalResult.Failure(
                $"Cannot submit result for contest in status: {contest.Status}");
        }

        // Submit the contest for approval
        contest.SubmitForApproval();

        _contestRepository.Update(contest);
        await _contestRepository.SaveChangesAsync();

        return ResultApprovalResult.Submitted(contestId);
    }

    /// <summary>
    /// Approves a contest result.
    /// </summary>
    public async Task<ResultApprovalResult> ApproveResultAsync(
        Guid contestId,
        Guid tenantId,
        Guid approvedBy,
        string? remarks = null)
    {
        var contest = await _contestRepository.GetByIdAsync(contestId);
        if (contest == null)
        {
            return ResultApprovalResult.Failure("Contest not found.");
        }

        if (contest.TenantId != tenantId)
        {
            return ResultApprovalResult.Failure("Contest does not belong to this tenant.");
        }

        if (contest.Status != ContestStatus.PendingApproval)
        {
            return ResultApprovalResult.Failure(
                $"Cannot approve contest in status: {contest.Status}");
        }

        // Approve the contest
        contest.Approve();

        // Update all result records for this contest
        var results = await _resultRepository.GetByContestIdAsync(contestId);
        foreach (var result in results)
        {
            result.Approve(approvedBy);
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                result.SetRemarks(remarks);
            }
            _resultRepository.Update(result);
        }

        _contestRepository.Update(contest);
        await _contestRepository.SaveChangesAsync();

        return ResultApprovalResult.Approved(contestId);
    }

    /// <summary>
    /// Rejects a contest result with a reason.
    /// </summary>
    public async Task<ResultApprovalResult> RejectResultAsync(
        Guid contestId,
        Guid tenantId,
        string rejectionReason,
        Guid rejectedBy)
    {
        var contest = await _contestRepository.GetByIdAsync(contestId);
        if (contest == null)
        {
            return ResultApprovalResult.Failure("Contest not found.");
        }

        if (contest.TenantId != tenantId)
        {
            return ResultApprovalResult.Failure("Contest does not belong to this tenant.");
        }

        if (contest.Status != ContestStatus.PendingApproval)
        {
            return ResultApprovalResult.Failure(
                $"Cannot reject contest in status: {contest.Status}");
        }

        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            return ResultApprovalResult.Failure("Rejection reason is required.");
        }

        // Update all result records for this contest with rejection
        var results = await _resultRepository.GetByContestIdAsync(contestId);
        foreach (var result in results)
        {
            result.Reject(rejectionReason);
            _resultRepository.Update(result);
        }

        // Transition contest back to completed (correction needed)
        contest.Complete();

        _contestRepository.Update(contest);
        await _contestRepository.SaveChangesAsync();

        return ResultApprovalResult.Rejected(contestId, rejectionReason);
    }

    /// <summary>
    /// Publishes an approved contest result.
    /// </summary>
    public async Task<ResultApprovalResult> PublishResultAsync(
        Guid contestId,
        Guid tenantId)
    {
        var contest = await _contestRepository.GetByIdAsync(contestId);
        if (contest == null)
        {
            return ResultApprovalResult.Failure("Contest not found.");
        }

        if (contest.TenantId != tenantId)
        {
            return ResultApprovalResult.Failure("Contest does not belong to this tenant.");
        }

        if (contest.Status != ContestStatus.Approved)
        {
            return ResultApprovalResult.Failure(
                $"Cannot publish contest in status: {contest.Status}");
        }

        // Publish all result records for this contest
        var results = await _resultRepository.GetByContestIdAsync(contestId);
        foreach (var result in results)
        {
            result.Publish();
            _resultRepository.Update(result);
        }

        _contestRepository.Update(contest);
        await _contestRepository.SaveChangesAsync();

        return ResultApprovalResult.Published(contestId);
    }

    /// <summary>
    /// Reopens a contest for corrections.
    /// </summary>
    public async Task<ResultApprovalResult> ReopenForCorrectionAsync(
        Guid contestId,
        Guid tenantId,
        string reason,
        Guid reopenedBy)
    {
        var contest = await _contestRepository.GetByIdAsync(contestId);
        if (contest == null)
        {
            return ResultApprovalResult.Failure("Contest not found.");
        }

        if (contest.TenantId != tenantId)
        {
            return ResultApprovalResult.Failure("Contest does not belong to this tenant.");
        }

        // Can only reopen approved or published results
        if (contest.Status != ContestStatus.Approved && contest.Status != ContestStatus.Published)
        {
            return ResultApprovalResult.Failure(
                $"Cannot reopen contest in status: {contest.Status}");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return ResultApprovalResult.Failure("Reopening reason is required.");
        }

        // Transition back to corrected status
        contest.SubmitForApproval(); // This will set to PendingApproval

        _contestRepository.Update(contest);
        await _contestRepository.SaveChangesAsync();

        return ResultApprovalResult.Reopened(contestId, reason);
    }

    /// <summary>
    /// Gets the approval status summary for a contest.
    /// </summary>
    public async Task<ResultApprovalStatus> GetApprovalStatusAsync(Guid contestId)
    {
        var contest = await _contestRepository.GetByIdAsync(contestId);
        if (contest == null)
        {
            return new ResultApprovalStatus(
                ContestId: contestId,
                ContestStatus: null,
                TotalResults: 0,
                PendingResults: 0,
                ApprovedResults: 0,
                RejectedResults: 0,
                PublishedResults: 0,
                CanSubmit: false,
                CanApprove: false,
                CanPublish: false,
                CanReject: false);
        }

        var results = await _resultRepository.GetByContestIdAsync(contestId);
        
        return new ResultApprovalStatus(
            ContestId: contestId,
            ContestStatus: contest.Status,
            TotalResults: results.Count,
            PendingResults: results.Count(r => r.Status == ContestResultStatus.Pending),
            ApprovedResults: results.Count(r => r.Status == ContestResultStatus.Approved),
            RejectedResults: results.Count(r => r.Status == ContestResultStatus.Rejected),
            PublishedResults: results.Count(r => r.Status == ContestResultStatus.Published),
            CanSubmit: contest.Status == ContestStatus.Completed || contest.Status == ContestStatus.Corrected,
            CanApprove: contest.Status == ContestStatus.PendingApproval,
            CanPublish: contest.Status == ContestStatus.Approved,
            CanReject: contest.Status == ContestStatus.PendingApproval);
    }
}

/// <summary>
/// Result of a result approval workflow operation.
/// </summary>
public class ResultApprovalResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorMessage { get; private init; }
    public Guid? ContestId { get; private init; }
    public string? Reason { get; private init; }
    public ResultApprovalResultType ResultType { get; private init; }

    private ResultApprovalResult() { }

    public static ResultApprovalResult Failure(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message,
        ResultType = ResultApprovalResultType.Failure
    };

    public static ResultApprovalResult Submitted(Guid contestId) => new()
    {
        IsSuccess = true,
        ContestId = contestId,
        ResultType = ResultApprovalResultType.Submitted
    };

    public static ResultApprovalResult Approved(Guid contestId) => new()
    {
        IsSuccess = true,
        ContestId = contestId,
        ResultType = ResultApprovalResultType.Approved
    };

    public static ResultApprovalResult Rejected(Guid contestId, string reason) => new()
    {
        IsSuccess = true,
        ContestId = contestId,
        Reason = reason,
        ResultType = ResultApprovalResultType.Rejected
    };

    public static ResultApprovalResult Published(Guid contestId) => new()
    {
        IsSuccess = true,
        ContestId = contestId,
        ResultType = ResultApprovalResultType.Published
    };

    public static ResultApprovalResult Reopened(Guid contestId, string reason) => new()
    {
        IsSuccess = true,
        ContestId = contestId,
        Reason = reason,
        ResultType = ResultApprovalResultType.Reopened
    };
}

public enum ResultApprovalResultType
{
    Failure,
    Submitted,
    Approved,
    Rejected,
    Published,
    Reopened
}

/// <summary>
/// Status summary for result approval workflow.
/// </summary>
public record ResultApprovalStatus(
    Guid ContestId,
    ContestStatus? ContestStatus,
    int TotalResults,
    int PendingResults,
    int ApprovedResults,
    int RejectedResults,
    int PublishedResults,
    bool CanSubmit,
    bool CanApprove,
    bool CanPublish,
    bool CanReject);