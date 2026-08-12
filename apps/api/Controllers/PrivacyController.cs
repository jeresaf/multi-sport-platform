using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/privacy")]
[Authorize]
public sealed class PrivacyController : ControllerBase
{
    private readonly SportsPlatformDbContext _database;
    private readonly IAuditService _audit;
    private readonly PrivacyRetentionService _retention;

    public PrivacyController(
        SportsPlatformDbContext database,
        IAuditService audit,
        PrivacyRetentionService retention)
    {
        _database = database;
        _audit = audit;
        _retention = retention;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyPrivacy(CancellationToken cancellationToken)
    {
        var context = await GetCurrentUserAsync(cancellationToken);
        if (context is null) return Unauthorized();
        var settings = await GetOrCreateSettingsAsync(context.Value.User, cancellationToken);
        var consents = await _database.ConsentRecords.AsNoTracking()
            .Where(item => item.UserId == context.Value.User.Id)
            .OrderByDescending(item => item.OccurredAt)
            .ToArrayAsync(cancellationToken);
        var requests = await _database.PrivacyActionRequests.AsNoTracking()
            .Where(item => item.UserId == context.Value.User.Id)
            .OrderByDescending(item => item.RequestedAt)
            .ToArrayAsync(cancellationToken);
        return Ok(new
        {
            profile = ProjectProfile(context.Value.User),
            settings,
            consents,
            requests = requests.Select(ProjectRequest)
        });
    }

    [HttpPut("consents/{kind}")]
    public async Task<IActionResult> RecordConsent(
        ConsentKind kind,
        [FromBody] ConsentRequest request,
        CancellationToken cancellationToken)
    {
        var context = await GetCurrentUserAsync(cancellationToken);
        if (context is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.PolicyVersion))
            return BadRequest(new { error = "Policy version is required." });

        var record = new ConsentRecord(
            Guid.NewGuid(), context.Value.User.Id, context.Value.User.TenantId, kind,
            request.Granted, request.PolicyVersion, "AdminWeb",
            HttpContext.Connection.RemoteIpAddress?.ToString());
        _database.ConsentRecords.Add(record);
        await _database.SaveChangesAsync(cancellationToken);
        await AuditAsync(
            request.Granted ? "PrivacyConsentGranted" : "PrivacyConsentRevoked",
            nameof(ConsentRecord), record.Id, context.Value.User,
            new { kind, request.Granted, request.PolicyVersion },
            request.Reason ?? $"{kind} consent preference updated.", cancellationToken);
        return Ok(record);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] PrivacySettingsRequest request,
        CancellationToken cancellationToken)
    {
        var context = await GetCurrentUserAsync(cancellationToken);
        if (context is null) return Unauthorized();
        var settings = await GetOrCreateSettingsAsync(context.Value.User, cancellationToken);
        var original = new { settings.ProfileVisibility, settings.ShowActivity, settings.AllowAnalytics, settings.AllowMarketing };
        settings.Update(request.ProfileVisibility, request.ShowActivity, request.AllowAnalytics, request.AllowMarketing);
        await _database.SaveChangesAsync(cancellationToken);
        await AuditAsync("PrivacySettingsUpdated", nameof(PrivacyControlSettings), settings.Id,
            context.Value.User,
            new { original, current = new { settings.ProfileVisibility, settings.ShowActivity, settings.AllowAnalytics, settings.AllowMarketing } },
            request.Reason ?? "Privacy and public visibility preferences updated.", cancellationToken);
        return Ok(settings);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> CorrectProfile(
        [FromBody] ProfileCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await GetCurrentUserAsync(cancellationToken);
        if (context is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "A correction reason is required." });
        var original = ProjectProfile(context.Value.User);
        try { context.Value.User.CorrectProfile(request.FirstName, request.LastName, request.PhoneNumber); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        await _database.SaveChangesAsync(cancellationToken);

        var workflow = new PrivacyActionRequest(
            Guid.NewGuid(), context.Value.User.Id, context.Value.User.TenantId,
            PrivacyRequestKind.DataCorrection,
            JsonSerializer.Serialize(new { request.FirstName, request.LastName, request.PhoneNumber, request.Reason }),
            JsonSerializer.Serialize(new { corrected = true }));
        _database.PrivacyActionRequests.Add(workflow);
        await _database.SaveChangesAsync(cancellationToken);
        await AuditAsync("PrivacyDataCorrected", nameof(User), context.Value.User.Id,
            context.Value.User, new { original, current = ProjectProfile(context.Value.User) }, request.Reason, cancellationToken);
        return Ok(new { profile = ProjectProfile(context.Value.User), request = ProjectRequest(workflow) });
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest(
        [FromBody] CreatePrivacyRequest request,
        CancellationToken cancellationToken)
    {
        var context = await GetCurrentUserAsync(cancellationToken);
        if (context is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Details))
            return BadRequest(new { error = "Request details are required." });

        string? payload = null;
        if (request.Kind == PrivacyRequestKind.DataAccess)
            payload = await BuildDataAccessPayloadAsync(context.Value.User, cancellationToken);
        var workflow = new PrivacyActionRequest(
            Guid.NewGuid(), context.Value.User.Id, context.Value.User.TenantId,
            request.Kind, request.Details, payload);
        _database.PrivacyActionRequests.Add(workflow);
        await _database.SaveChangesAsync(cancellationToken);
        await AuditAsync("PrivacyRequestCreated", nameof(PrivacyActionRequest), workflow.Id,
            context.Value.User, new { request.Kind, workflow.Status }, request.Details, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ProjectRequest(workflow));
    }

    [HttpGet("requests/{id:guid}/download")]
    public async Task<IActionResult> DownloadDataAccess(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        var request = await _database.PrivacyActionRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (request is null) return NotFound();
        if (request.Kind != PrivacyRequestKind.DataAccess
            || request.Status != PrivacyRequestState.Completed
            || string.IsNullOrWhiteSpace(request.ExportPayload))
            return Conflict(new { error = "The data access package is not available." });
        await _audit.RecordAsync("PrivacyDataDownloaded", nameof(PrivacyActionRequest), id.ToString(),
            request.TenantId, userId, reason: "The data subject downloaded their access package.",
            cancellationToken: cancellationToken);
        return File(Encoding.UTF8.GetBytes(request.ExportPayload), "application/json", $"privacy-data-{id:N}.json");
    }

    [HttpGet("admin/requests")]
    [RequirePermission("privacy.read")]
    public async Task<IActionResult> ListRequests([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        var requests = await _database.PrivacyActionRequests.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.RequestedAt)
            .ToArrayAsync(cancellationToken);
        var userIds = requests.Select(item => item.UserId).Distinct().ToArray();
        var users = await _database.Users.AsNoTracking().Where(item => userIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        return Ok(requests.Select(item => new { request = ProjectRequest(item), user = users.TryGetValue(item.UserId, out var user) ? ProjectProfile(user) : null }));
    }

    [HttpPut("admin/requests/{id:guid}/decision")]
    [RequirePermission("privacy.manage")]
    public async Task<IActionResult> DecideRequest(Guid id, [FromBody] PrivacyDecisionRequest decision, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(decision.Response)) return BadRequest(new { error = "A decision response is required." });
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        var request = await _database.PrivacyActionRequests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (request is null) return NotFound();
        if (!request.TenantId.HasValue || !User.CanAccessTenant(request.TenantId.Value)) return Forbid();
        try { request.Review(actorId.Value, decision.Approved, decision.Response); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        await _database.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync(decision.Approved ? "PrivacyRequestApproved" : "PrivacyRequestRejected",
            nameof(PrivacyActionRequest), request.Id.ToString(), request.TenantId, actorId,
            newValue: JsonSerializer.Serialize(new { request.Kind, request.Status, decision.Response }),
            reason: decision.Response, cancellationToken: cancellationToken);
        return Ok(ProjectRequest(request));
    }

    [HttpPut("admin/requests/{id:guid}/complete")]
    [RequirePermission("privacy.manage")]
    public async Task<IActionResult> CompleteRequest(Guid id, [FromBody] CompletePrivacyRequest completion, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(completion.Response)) return BadRequest(new { error = "A completion response is required." });
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        var request = await _database.PrivacyActionRequests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (request is null) return NotFound();
        if (!request.TenantId.HasValue || !User.CanAccessTenant(request.TenantId.Value)) return Forbid();
        if (request.Kind == PrivacyRequestKind.AccountDeletion)
        {
            var subject = await _database.Users.Include(item => item.Sessions)
                .SingleAsync(item => item.Id == request.UserId, cancellationToken);
            subject.RevokeAllSessions();
            subject.AnonymizeForPrivacy();
        }
        try { request.Complete(completion.Response); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        await _database.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("PrivacyRequestCompleted", nameof(PrivacyActionRequest), request.Id.ToString(),
            request.TenantId, actorId,
            newValue: JsonSerializer.Serialize(new { request.Kind, request.Status }),
            reason: completion.Response, cancellationToken: cancellationToken);
        return Ok(ProjectRequest(request));
    }

    [HttpGet("admin/retention")]
    [RequirePermission("privacy.read")]
    public async Task<IActionResult> ListRetention([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        return Ok(await _database.TenantRetentionPolicies.AsNoTracking()
            .Where(item => item.TenantId == tenantId).OrderBy(item => item.Resource)
            .ToArrayAsync(cancellationToken));
    }

    [HttpPost("admin/retention")]
    [RequirePermission("privacy.manage")]
    public async Task<IActionResult> CreateRetention([FromBody] RetentionPolicyRequest request, CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(request.TenantId)) return Forbid();
        if (await _database.TenantRetentionPolicies.AnyAsync(item => item.TenantId == request.TenantId && item.Resource == request.Resource, cancellationToken))
            return Conflict(new { error = "A retention policy already exists for that resource." });
        TenantRetentionPolicy policy;
        try { policy = new(Guid.NewGuid(), request.TenantId, request.Name, request.Resource, request.RetentionDays, request.Action, request.RunIntervalHours, request.IsActive); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return BadRequest(new { error = exception.Message }); }
        _database.TenantRetentionPolicies.Add(policy);
        await _database.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("PrivacyRetentionPolicyCreated", nameof(TenantRetentionPolicy), policy.Id.ToString(),
            policy.TenantId, User.UserId(), newValue: JsonSerializer.Serialize(request),
            reason: request.Reason ?? "Tenant retention policy created.", cancellationToken: cancellationToken);
        return StatusCode(StatusCodes.Status201Created, policy);
    }

    [HttpPut("admin/retention/{id:guid}")]
    [RequirePermission("privacy.manage")]
    public async Task<IActionResult> UpdateRetention(Guid id, [FromBody] RetentionPolicyRequest request, CancellationToken cancellationToken)
    {
        var policy = await _database.TenantRetentionPolicies.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (policy is null) return NotFound();
        if (policy.TenantId != request.TenantId || !User.CanAccessTenant(policy.TenantId)) return Forbid();
        var original = JsonSerializer.Serialize(new { policy.Name, policy.Resource, policy.RetentionDays, policy.Action, policy.RunIntervalHours, policy.IsActive });
        try { policy.Update(request.Name, request.Resource, request.RetentionDays, request.Action, request.RunIntervalHours, request.IsActive); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return BadRequest(new { error = exception.Message }); }
        await _database.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("PrivacyRetentionPolicyUpdated", nameof(TenantRetentionPolicy), policy.Id.ToString(),
            policy.TenantId, User.UserId(), original, JsonSerializer.Serialize(request),
            request.Reason ?? "Tenant retention policy updated.", cancellationToken);
        return Ok(policy);
    }

    [HttpDelete("admin/retention/{id:guid}")]
    [RequirePermission("privacy.manage")]
    public async Task<IActionResult> DeleteRetention(Guid id, [FromQuery] string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason)) return BadRequest(new { error = "A deletion reason is required." });
        var policy = await _database.TenantRetentionPolicies.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (policy is null) return NotFound();
        if (!User.CanAccessTenant(policy.TenantId)) return Forbid();
        _database.TenantRetentionPolicies.Remove(policy);
        await _database.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("PrivacyRetentionPolicyDeleted", nameof(TenantRetentionPolicy), id.ToString(),
            policy.TenantId, User.UserId(), originalValue: JsonSerializer.Serialize(new { policy.Name, policy.Resource }),
            reason: reason, cancellationToken: cancellationToken);
        return NoContent();
    }

    [HttpPost("admin/retention/{id:guid}/run")]
    [RequirePermission("privacy.manage")]
    public async Task<IActionResult> RunRetention(Guid id, [FromBody] RunRetentionRequest request, CancellationToken cancellationToken)
    {
        var policy = await _database.TenantRetentionPolicies.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (policy is null) return NotFound();
        if (!User.CanAccessTenant(policy.TenantId)) return Forbid();
        var affected = await _retention.ExecuteAsync(policy, User.UserId(), request.Reason ?? "Manual tenant retention run", cancellationToken);
        return Ok(new { affected, policy.LastRunAt, policy.NextRunAt });
    }

    private async Task<(User User, Guid UserId)?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = User.UserId();
        if (!userId.HasValue) return null;
        var user = await _database.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        return user is null ? null : (user, userId.Value);
    }

    private async Task<PrivacyControlSettings> GetOrCreateSettingsAsync(User user, CancellationToken cancellationToken)
    {
        var settings = await _database.PrivacyControlSettings.SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (settings is not null) return settings;
        settings = new PrivacyControlSettings(Guid.NewGuid(), user.Id, user.TenantId);
        _database.PrivacyControlSettings.Add(settings);
        await _database.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private async Task<string> BuildDataAccessPayloadAsync(User user, CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateSettingsAsync(user, cancellationToken);
        var consents = await _database.ConsentRecords.AsNoTracking().Where(item => item.UserId == user.Id).ToArrayAsync(cancellationToken);
        var roles = await _database.UserRoles.AsNoTracking().Where(item => item.UserId == user.Id)
            .Join(_database.Roles.AsNoTracking(), item => item.RoleId, role => role.Id, (_, role) => role.Name)
            .ToArrayAsync(cancellationToken);
        return JsonSerializer.Serialize(new
        {
            generatedAt = DateTime.UtcNow,
            profile = ProjectProfile(user),
            roles,
            privacySettings = settings,
            consentHistory = consents
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private Task AuditAsync(string action, string entityType, Guid entityId, User user, object value, string reason, CancellationToken cancellationToken) =>
        _audit.RecordAsync(action, entityType, entityId.ToString(), user.TenantId, user.Id,
            newValue: JsonSerializer.Serialize(value), reason: reason, cancellationToken: cancellationToken);

    private static object ProjectProfile(User user) => new { user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber, user.TenantId };
    private static object ProjectRequest(PrivacyActionRequest request) => new
    {
        request.Id, request.UserId, request.TenantId, request.Kind, request.Status,
        request.Details, request.Response, request.RequestedAt, request.ReviewedAt,
        request.ReviewedBy, request.CompletedAt,
        downloadAvailable = request.Kind == PrivacyRequestKind.DataAccess
            && request.Status == PrivacyRequestState.Completed
            && request.ExportPayload != null
    };
}

public sealed record ConsentRequest(bool Granted, string PolicyVersion, string? Reason);
public sealed record PrivacySettingsRequest(ProfileVisibilityLevel ProfileVisibility, bool ShowActivity, bool AllowAnalytics, bool AllowMarketing, string? Reason);
public sealed record ProfileCorrectionRequest(string FirstName, string LastName, string? PhoneNumber, string Reason);
public sealed record CreatePrivacyRequest(PrivacyRequestKind Kind, string Details);
public sealed record PrivacyDecisionRequest(bool Approved, string Response);
public sealed record CompletePrivacyRequest(string Response);
public sealed record RetentionPolicyRequest(Guid TenantId, string Name, RetentionResource Resource, int RetentionDays, RetentionAction Action, int RunIntervalHours, bool IsActive, string? Reason);
public sealed record RunRetentionRequest(string? Reason);
