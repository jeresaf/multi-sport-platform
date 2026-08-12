using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Modules.Identity.Entities;
using SportsPlatform.Modules.Registrations.Entities;
using SportsPlatform.Modules.Registrations.Services;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Modules.Notifications.Entities;
using SportsPlatform.Modules.Notifications.Services;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RegistrationsController : ControllerBase
{
    private readonly IRegistrationService _registrationService;
    private readonly SportsPlatformDbContext _database;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly ICompetitionEligibilityService _eligibility;

    public RegistrationsController(
        IRegistrationService registrationService,
        SportsPlatformDbContext database,
        IAuditService audit,
        INotificationService notifications,
        ICompetitionEligibilityService eligibility)
    {
        _registrationService = registrationService;
        _database = database;
        _audit = audit;
        _notifications = notifications;
        _eligibility = eligibility;
    }

    /// <summary>
    /// Create a new registration for a contest
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateRegistration(
        [FromBody] CreateRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!await User.RegistrationResourcesShareTenantAsync(
                _database,
                request.ContestId,
                request.ParticipantId,
                cancellationToken))
            return Forbid();

        var registration = await _registrationService.CreateRegistrationAsync(
            request.ContestId,
            request.ParticipantId,
            request.Type,
            cancellationToken);
        await AuditAsync(registration, "RegistrationCreated", "Registration created", cancellationToken);

        return CreatedAtAction(
            nameof(GetRegistration),
            new { id = registration.Id },
            registration);
    }

    [HttpPost("competition/{competitionId:guid}")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> CreateCompetitionRegistration(
        Guid competitionId,
        [FromBody] CreateCompetitionRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!await User.CompetitionRegistrationResourcesShareTenantAsync(
                _database,
                competitionId,
                request.ParticipantId,
                cancellationToken))
            return Forbid();

        var registration = await _registrationService.CreateCompetitionRegistrationAsync(
            competitionId,
            request.ParticipantId,
            request.Type,
            cancellationToken);
        await AuditAsync(registration, "RegistrationCreated", "Competition registration created", cancellationToken);
        return CreatedAtAction(
            nameof(GetRegistration),
            new { id = registration.Id },
            registration);
    }

    [HttpGet("competition/{competitionId:guid}")]
    [RequirePermission("competitions.read")]
    public async Task<IActionResult> GetRegistrationsByCompetition(
        Guid competitionId,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessCompetitionAsync(_database, competitionId, cancellationToken))
            return Forbid();

        return Ok(await _registrationService.GetRegistrationsByCompetitionAsync(
            competitionId,
            cancellationToken));
    }

    [HttpGet("competition/{competitionId:guid}/review-queue")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> GetCompetitionReviewQueue(
        Guid competitionId,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessCompetitionAsync(_database, competitionId, cancellationToken))
            return Forbid();
        var competition = await _database.Competitions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == competitionId, cancellationToken);
        if (competition is null) return NotFound();
        var registrations = await _database.Registrations.AsNoTracking()
            .Where(item => item.CompetitionId == competitionId)
            .OrderBy(item => item.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var participantIds = registrations.Select(item => item.ParticipantId).Distinct().ToArray();
        var participants = await _database.Participants.AsNoTracking()
            .Where(item => participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var reviewerIds = registrations.Where(item => item.ReviewedBy.HasValue)
            .Select(item => item.ReviewedBy!.Value).Distinct().ToArray();
        var reviewers = await _database.Users.AsNoTracking()
            .Where(item => reviewerIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        return Ok(new
        {
            competition = new { competition.Id, competition.Name, competition.TenantId },
            counts = Enum.GetValues<RegistrationStatus>().ToDictionary(
                status => status.ToString(),
                status => registrations.Count(item => item.Status == status)),
            registrations = registrations.Select(item => new
            {
                item.Id,
                item.ParticipantId,
                participantName = participants.GetValueOrDefault(item.ParticipantId)?.Name
                    ?? item.ParticipantId.ToString(),
                participantType = participants.GetValueOrDefault(item.ParticipantId)?.Type.ToString()
                    ?? item.Type.ToString(),
                status = item.Status.ToString(),
                type = item.Type.ToString(),
                item.CreatedAt,
                item.RegisteredAt,
                item.SubmittedBy,
                item.ReviewedBy,
                reviewerName = item.ReviewedBy.HasValue
                    && reviewers.TryGetValue(item.ReviewedBy.Value, out var reviewer)
                        ? $"{reviewer.FirstName} {reviewer.LastName}".Trim()
                        : null,
                item.ReviewedAt,
                eligibilityIssues = item.EligibilityIssues(),
                item.EligibilityCheckedAt,
                item.EligibilityOverriddenBy,
                item.EligibilityOverriddenAt,
                item.EligibilityOverrideReason,
                reason = item.Status == RegistrationStatus.Withdrawn
                    ? item.CancellationReason : item.ReviewNotes,
                eligible = item.Status == RegistrationStatus.Approved
            })
        });
    }

    [HttpGet("competition/{competitionId:guid}/eligibility-policy")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> GetEligibilityPolicy(Guid competitionId, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessCompetitionAsync(_database, competitionId, cancellationToken)) return Forbid();
        var policy = await _database.CompetitionEligibilityPolicies.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CompetitionId == competitionId, cancellationToken);
        return Ok(policy);
    }

    [HttpPut("competition/{competitionId:guid}/eligibility-policy")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> PutEligibilityPolicy(Guid competitionId,
        [FromBody] PutEligibilityPolicyRequest request, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessCompetitionAsync(_database, competitionId, cancellationToken)) return Forbid();
        var competition = await _database.Competitions.SingleOrDefaultAsync(item => item.Id == competitionId, cancellationToken);
        if (competition is null) return NotFound();
        try
        {
            var policy = await _database.CompetitionEligibilityPolicies
                .SingleOrDefaultAsync(item => item.CompetitionId == competitionId, cancellationToken);
            if (policy is null)
            {
                policy = new CompetitionEligibilityPolicy(Guid.NewGuid(), competition.TenantId, competitionId);
                _database.CompetitionEligibilityPolicies.Add(policy);
            }
            policy.Configure(request.MinimumSquadSize, request.MaximumSquadSize,
                request.MinimumAge, request.MaximumAge, request.MinimumWeightKg, request.MaximumWeightKg,
                request.RegistrationDeadline, request.RequiredDocumentTypes,
                request.RequireSquadNumbers, request.RequireUniqueSquadNumbers);
            await _database.SaveChangesAsync(cancellationToken);
            await _audit.RecordAsync("CompetitionEligibilityPolicyUpdated", "CompetitionEligibilityPolicy",
                policy.Id.ToString(), competition.TenantId, User.UserId(), null,
                System.Text.Json.JsonSerializer.Serialize(request), "Eligibility rules updated", cancellationToken);
            return Ok(policy);
        }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpGet("{id:guid}/eligibility")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> GetEligibility(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationAsync(_database, id, cancellationToken)) return Forbid();
        var registration = await _database.Registrations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return registration is null ? NotFound() : Ok(await _eligibility.EvaluateAsync(registration, cancellationToken));
    }

    /// <summary>
    /// Get a registration by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRegistration(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationAsync(_database, id, cancellationToken))
            return Forbid();

        var registration = await _registrationService.GetRegistrationAsync(id, cancellationToken);
        if (registration == null)
            return NotFound();

        return Ok(registration);
    }

    /// <summary>
    /// Get all registrations for a contest
    /// </summary>
    [HttpGet("contest/{contestId:guid}")]
    public async Task<IActionResult> GetRegistrationsByContest(Guid contestId, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessContestAsync(_database, contestId, cancellationToken))
            return Forbid();

        var registrations = await _registrationService.GetRegistrationsByContestAsync(contestId, cancellationToken);
        return Ok(registrations);
    }

    /// <summary>
    /// Get all registrations for a participant
    /// </summary>
    [HttpGet("participant/{participantId:guid}")]
    public async Task<IActionResult> GetRegistrationsByParticipant(Guid participantId, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessParticipantAsync(_database, participantId, cancellationToken))
            return Forbid();

        var registrations = await _registrationService.GetRegistrationsByParticipantAsync(participantId, cancellationToken);
        return Ok(registrations);
    }

    /// <summary>
    /// Submit a registration (move from Draft to Submitted)
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> SubmitRegistration(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var submittedBy = User.UserId();
            if (!submittedBy.HasValue) return Unauthorized();
            var registration = await _registrationService.SubmitRegistrationAsync(
                id, submittedBy.Value, cancellationToken);
            await AuditAsync(registration, "RegistrationSubmitted", "Registration submitted", cancellationToken);
            await NotifyAsync(registration, "Registration submitted",
                "Your competition entry was submitted and is awaiting official review.",
                "Registration submitted", cancellationToken);
            return Ok(registration);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Start review of a registration (move from Submitted to UnderReview)
    /// </summary>
    [HttpPost("{id:guid}/review")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> StartReview(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var registration = await _registrationService.StartReviewAsync(id, cancellationToken);
            await AuditAsync(registration, "RegistrationReviewStarted", "Registration review started", cancellationToken);
            await NotifyAsync(registration, "Registration under review",
                "An official has started reviewing your competition entry.",
                "Registration review started", cancellationToken);
            return Ok(registration);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Approve a registration
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> ApproveRegistration(
        Guid id,
        [FromBody] ApproveRegistrationRequest? request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var reviewerId = User.UserId();
            if (!reviewerId.HasValue) return Unauthorized();
            var registrationToReview = await _database.Registrations
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (registrationToReview is null) return NotFound();
            var evaluation = await _eligibility.EvaluateAsync(registrationToReview, cancellationToken);
            if (!evaluation.IsEligible && request?.OverrideEligibility == true && !CanOverrideEligibility())
                return Forbid();
            registrationToReview.RecordEligibility(evaluation.Issues,
                !evaluation.IsEligible && request?.OverrideEligibility == true ? reviewerId : null,
                request?.OverrideReason);
            await _database.SaveChangesAsync(cancellationToken);
            await AuditAsync(registrationToReview, "RegistrationEligibilityChecked",
                evaluation.IsEligible ? "Eligibility checks passed" : string.Join(" | ", evaluation.Issues), cancellationToken);
            if (!evaluation.IsEligible && request?.OverrideEligibility != true)
                return BadRequest(new { error = "The registration is not eligible for approval.", eligibility = evaluation });
            if (!evaluation.IsEligible)
                await AuditAsync(registrationToReview, "RegistrationEligibilityOverridden", request?.OverrideReason, cancellationToken);
            var registration = await _registrationService.ApproveRegistrationAsync(id, reviewerId.Value, request?.Notes, cancellationToken);
            await AuditAsync(registration, "RegistrationApproved", request?.Notes, cancellationToken);
            await NotifyAsync(registration, "Registration approved",
                "Your competition entry was approved and is now eligible to participate.",
                registration.ReviewNotes, cancellationToken);
            return Ok(registration);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private bool CanOverrideEligibility() =>
        User.IsInRole(SystemRoles.PlatformOwner) || User.IsInRole("Admin")
        || User.HasClaim("permission", "registrations.override");

    /// <summary>
    /// Reject a registration
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> RejectRegistration(
        Guid id,
        [FromBody] RejectRegistrationRequest? request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var reviewerId = User.UserId();
            if (!reviewerId.HasValue) return Unauthorized();
            var registration = await _registrationService.RejectRegistrationAsync(id, reviewerId.Value, request?.Reason, cancellationToken);
            await AuditAsync(registration, "RegistrationRejected", request?.Reason, cancellationToken);
            await NotifyAsync(registration, "Registration rejected",
                "Your competition entry was rejected. Review the recorded reason for details.",
                registration.ReviewNotes, cancellationToken);
            return Ok(registration);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Add a registration to waitlist
    /// </summary>
    [HttpPost("{id:guid}/waitlist")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> WaitlistRegistration(
        Guid id,
        [FromBody] WaitlistRegistrationRequest? request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var reviewerId = User.UserId();
            if (!reviewerId.HasValue) return Unauthorized();
            var registration = await _registrationService.WaitlistRegistrationAsync(id, reviewerId.Value, request?.Notes, cancellationToken);
            await AuditAsync(registration, "RegistrationWaitlisted", request?.Notes, cancellationToken);
            await NotifyAsync(registration, "Registration waitlisted",
                "Your competition entry was placed on the waitlist.",
                registration.ReviewNotes, cancellationToken);
            return Ok(registration);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Confirm a registration
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> ConfirmRegistration(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var registration = await _registrationService.ConfirmRegistrationAsync(id, cancellationToken);
            await AuditAsync(registration, "RegistrationConfirmed", "Registration confirmed", cancellationToken);
            return Ok(registration);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Withdraw a registration
    /// </summary>
    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> WithdrawRegistration(
        Guid id,
        [FromBody] WithdrawRegistrationRequest? request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var registration = await _registrationService.WithdrawRegistrationAsync(id, request?.Reason, cancellationToken);
            await AuditAsync(registration, "RegistrationWithdrawn", request?.Reason, cancellationToken);
            await NotifyAsync(registration, "Registration withdrawn",
                "Your competition entry was withdrawn.",
                registration.CancellationReason, cancellationToken);
            return Ok(registration);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a registration
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> CancelRegistration(
        Guid id,
        [FromBody] CancelRegistrationRequest? request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var registration = await _registrationService.CancelRegistrationAsync(id, request?.Reason, cancellationToken);
            await AuditAsync(registration, "RegistrationCancelled", request?.Reason, cancellationToken);
            return Ok(registration);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Mark a registration as no-show
    /// </summary>
    [HttpPost("{id:guid}/no-show")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> MarkAsNoShow(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var registration = await _registrationService.MarkAsNoShowAsync(id, cancellationToken);
            await AuditAsync(registration, "RegistrationNoShow", "Participant marked as no-show", cancellationToken);
            return Ok(registration);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get registration statistics for a contest
    /// </summary>
    [HttpGet("contest/{contestId:guid}/stats")]
    public async Task<IActionResult> GetContestRegistrationStats(Guid contestId, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessContestAsync(_database, contestId, cancellationToken))
            return Forbid();

        var stats = await _registrationService.GetRegistrationStatsAsync(contestId, cancellationToken);
        return Ok(stats);
    }

    private async Task AuditAsync(
        Registration registration,
        string action,
        string? reason,
        CancellationToken cancellationToken)
    {
        Guid? tenantId = null;
        if (registration.CompetitionId.HasValue)
            tenantId = await _database.Competitions.AsNoTracking()
                .Where(item => item.Id == registration.CompetitionId.Value)
                .Select(item => (Guid?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken);
        else if (registration.ContestId.HasValue)
            tenantId = await _database.Contests.AsNoTracking()
                .Where(item => item.Id == registration.ContestId.Value)
                .Select(item => (Guid?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken);
        await _audit.RecordAsync(
            action,
            "Registration",
            registration.Id.ToString(),
            tenantId,
            User.UserId(),
            null,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                status = registration.Status.ToString(),
                registration.ParticipantId,
                registration.CompetitionId,
                registration.ContestId
            }),
            reason,
            cancellationToken);
    }

    private async Task NotifyAsync(
        Registration registration,
        string title,
        string content,
        string? reason,
        CancellationToken cancellationToken)
    {
        var recipientId = registration.SubmittedBy ?? User.UserId();
        if (!recipientId.HasValue) return;
        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            registrationId = registration.Id,
            registration.CompetitionId,
            registration.ContestId,
            registration.ParticipantId,
            status = registration.Status.ToString(),
            reason,
            eligible = registration.Status == RegistrationStatus.Approved
        });
        await _notifications.CreateNotificationAsync(
            recipientId.Value,
            NotificationType.Registration,
            NotificationChannel.InApp,
            title,
            content,
            metadata,
            cancellationToken);
    }
}

public class CreateRegistrationRequest
{
    public Guid ContestId { get; set; }
    public Guid ParticipantId { get; set; }
    public RegistrationType Type { get; set; }
}

public class CreateCompetitionRegistrationRequest
{
    public Guid ParticipantId { get; set; }
    public RegistrationType Type { get; set; }
}

public class ApproveRegistrationRequest
{
    public string? Notes { get; set; }
    public bool OverrideEligibility { get; set; }
    public string? OverrideReason { get; set; }
}

public sealed class PutEligibilityPolicyRequest
{
    public int? MinimumSquadSize { get; set; }
    public int? MaximumSquadSize { get; set; }
    public int? MinimumAge { get; set; }
    public int? MaximumAge { get; set; }
    public decimal? MinimumWeightKg { get; set; }
    public decimal? MaximumWeightKg { get; set; }
    public DateTime? RegistrationDeadline { get; set; }
    public string[] RequiredDocumentTypes { get; set; } = [];
    public bool RequireSquadNumbers { get; set; }
    public bool RequireUniqueSquadNumbers { get; set; } = true;
}

public class RejectRegistrationRequest
{
    public string? Reason { get; set; }
}

public class WaitlistRegistrationRequest
{
    public string? Notes { get; set; }
}

public class WithdrawRegistrationRequest
{
    public string? Reason { get; set; }
}

public class CancelRegistrationRequest
{
    public string? Reason { get; set; }
}
