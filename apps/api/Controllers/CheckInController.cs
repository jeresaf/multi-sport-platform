using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Api.Hubs;
using SportsPlatform.Infrastructure.Services;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission("scoring.manage")]
public sealed class CheckInController : ControllerBase
{
    private readonly IContestCheckInWorkflowService _workflow;
    private readonly IContestsService _contests;
    private readonly ContestHubNotifier _notifier;

    public CheckInController(
        IContestCheckInWorkflowService workflow,
        IContestsService contests,
        ContestHubNotifier notifier)
    {
        _workflow = workflow;
        _contests = contests;
        _notifier = notifier;
    }

    [HttpPost("contests/{contestId:guid}/open")]
    public Task<IActionResult> Open(Guid contestId) =>
        Execute(contestId, actor => _workflow.OpenAsync(contestId, actor));

    [HttpPost("contests/{contestId:guid}/participants/{participantId:guid}/checkin")]
    public async Task<IActionResult> CheckInParticipant(Guid contestId, Guid participantId)
    {
        var result = await Execute(contestId, actor =>
            _workflow.CheckInParticipantAsync(contestId, participantId, actor));
        if (result is OkObjectResult)
            await BroadcastParticipantAsync(contestId, participantId, "CheckedIn", null);
        return result;
    }

    [HttpPost("contests/{contestId:guid}/participants/{participantId:guid}/withdraw")]
    public async Task<IActionResult> WithdrawParticipant(
        Guid contestId, Guid participantId, [FromBody] CheckInReasonRequest? request)
    {
        var result = await Execute(contestId, actor =>
            _workflow.WithdrawParticipantAsync(
                contestId, participantId, actor, request?.Reason ?? string.Empty));
        if (result is OkObjectResult)
            await BroadcastParticipantAsync(contestId, participantId, "Withdrawn", request?.Reason);
        return result;
    }

    [HttpPost("contests/{contestId:guid}/participants/{participantId:guid}/disqualify")]
    public async Task<IActionResult> DisqualifyParticipant(
        Guid contestId, Guid participantId, [FromBody] CheckInReasonRequest? request)
    {
        var result = await Execute(contestId, actor =>
            _workflow.DisqualifyParticipantAsync(
                contestId, participantId, actor, request?.Reason ?? string.Empty));
        if (result is OkObjectResult)
            await BroadcastParticipantAsync(contestId, participantId, "Disqualified", request?.Reason);
        return result;
    }

    [HttpPost("contests/{contestId:guid}/roster/confirm")]
    public Task<IActionResult> ConfirmRoster(Guid contestId) =>
        Execute(contestId, actor => _workflow.ConfirmRosterAsync(contestId, actor));

    [HttpPost("contests/{contestId:guid}/officials/{officialId:guid}/confirm")]
    public Task<IActionResult> ConfirmOfficial(Guid contestId, Guid officialId) =>
        Execute(contestId, actor =>
            _workflow.ConfirmOfficialAsync(contestId, officialId, actor));

    [HttpPost("contests/{contestId:guid}/venue/confirm")]
    public Task<IActionResult> ConfirmVenue(Guid contestId) =>
        Execute(contestId, actor => _workflow.ConfirmVenueAsync(contestId, actor));

    [HttpPost("contests/{contestId:guid}/complete")]
    public Task<IActionResult> Complete(Guid contestId) =>
        Execute(contestId, actor => _workflow.CompleteAsync(contestId, actor));

    [HttpPost("contests/{contestId:guid}/override")]
    [RequirePermission("contests.manage")]
    public Task<IActionResult> Override(
        Guid contestId, [FromBody] CheckInReasonRequest request) =>
        Execute(contestId, actor =>
            _workflow.OverrideAsync(contestId, actor, request.Reason));

    [HttpGet("contests/{contestId:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid contestId)
    {
        var forbidden = await AuthorizeContestAsync(contestId);
        if (forbidden is not null) return forbidden;
        var state = await _workflow.GetAsync(contestId);
        return state is null ? NotFound() : Ok(state);
    }

    [HttpGet("contests/{contestId:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid contestId)
    {
        var forbidden = await AuthorizeContestAsync(contestId);
        if (forbidden is not null) return forbidden;
        return Ok(await _workflow.HistoryAsync(contestId));
    }

    private async Task<IActionResult> Execute(
        Guid contestId,
        Func<Guid, Task<ContestReadinessState>> action)
    {
        var forbidden = await AuthorizeContestAsync(contestId);
        if (forbidden is not null) return forbidden;
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        try
        {
            return Ok(await action(actorId.Value));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    private async Task<IActionResult?> AuthorizeContestAsync(Guid contestId)
    {
        var contest = await _contests.GetContestByIdAsync(contestId);
        if (contest is null) return NotFound();
        return User.CanAccessTenant(contest.TenantId) ? null : Forbid();
    }

    private Task BroadcastParticipantAsync(
        Guid contestId, Guid participantId, string status, string? reason) =>
        _notifier.BroadcastParticipantStatusChange(
            contestId,
            participantId,
            null,
            new ParticipantStatusChangeDto(
                participantId, "Entered", status, reason, DateTime.UtcNow));
}

public sealed record CheckInReasonRequest(string Reason);
