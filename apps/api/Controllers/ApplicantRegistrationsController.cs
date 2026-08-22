using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Api.Services;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Registrations.Entities;
using SportsPlatform.Modules.Registrations.Services;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/applicant")]
[Authorize]
public sealed class ApplicantRegistrationsController : ControllerBase
{
    private readonly SportsPlatformDbContext _database;
    private readonly IAuditService _audit;
    private readonly IBrandingAssetStorage _assetStorage;

    public ApplicantRegistrationsController(
        SportsPlatformDbContext database,
        IAuditService audit,
        IBrandingAssetStorage assetStorage)
    {
        _database = database;
        _audit = audit;
        _assetStorage = assetStorage;
    }

    [HttpGet("participants")]
    public async Task<IActionResult> GetMyParticipants(CancellationToken cancellationToken)
    {
        var identity = Identity();
        if (identity.Result is not null) return identity.Result;
        var items = await _database.Participants.AsNoTracking()
            .Where(item => item.TenantId == identity.TenantId
                && item.ApplicantUserId == identity.UserId)
            .OrderBy(item => item.Name)
            .Select(item => new
            {
                item.Id, item.Name, item.ShortName,
                type = item.Type.ToString(), status = item.Status.ToString()
            })
            .ToArrayAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost("participants")]
    public async Task<IActionResult> CreateParticipant(
        [FromBody] CreateApplicantParticipantRequest request,
        CancellationToken cancellationToken)
    {
        var identity = Identity();
        if (identity.Result is not null) return identity.Result;
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ShortName))
            return BadRequest(new { error = "A participant name and short name are required." });

        Participant participant;
        if (request.Type.Equals("Individual", StringComparison.OrdinalIgnoreCase)
            || request.Type.Equals("Athlete", StringComparison.OrdinalIgnoreCase))
            participant = new Athlete(Guid.NewGuid(), identity.TenantId, request.Name, request.ShortName);
        else if (request.Type.Equals("Team", StringComparison.OrdinalIgnoreCase))
            participant = new Team(Guid.NewGuid(), identity.TenantId, request.Name, request.ShortName);
        else
            return BadRequest(new { error = "Applicant profiles currently support Individual or Team." });

        participant.AssignApplicant(identity.UserId);
        _database.Participants.Add(participant);
        await _database.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("ApplicantParticipantCreated", "Participant", participant.Id.ToString(),
            identity.TenantId, identity.UserId, null,
            JsonSerializer.Serialize(new { participant.Name, participant.ShortName, Type = participant.Type.ToString() }),
            "Applicant created a registration participant", cancellationToken);
        return CreatedAtAction(nameof(GetMyParticipants), new { }, new
        {
            participant.Id, participant.Name, participant.ShortName,
            type = participant.Type.ToString(), status = participant.Status.ToString()
        });
    }

    [HttpGet("registrations")]
    public async Task<IActionResult> GetMyRegistrations(CancellationToken cancellationToken)
    {
        var identity = Identity();
        if (identity.Result is not null) return identity.Result;
        var registrations = await _database.Registrations.AsNoTracking()
            .Where(item => item.ApplicantUserId == identity.UserId)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var competitionIds = registrations.Where(item => item.CompetitionId.HasValue)
            .Select(item => item.CompetitionId!.Value).Distinct().ToArray();
        var participantIds = registrations.Select(item => item.ParticipantId).Distinct().ToArray();
        var submissionIds = registrations.Where(item => item.FormSubmissionId.HasValue)
            .Select(item => item.FormSubmissionId!.Value).Distinct().ToArray();
        var competitions = await _database.Competitions.AsNoTracking()
            .Where(item => competitionIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var participants = await _database.Participants.AsNoTracking()
            .Where(item => participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var submissions = await _database.RegistrationFormSubmissions.AsNoTracking()
            .Where(item => submissionIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        return Ok(registrations.Select(item => new
        {
            item.Id,
            item.CompetitionId,
            competitionName = item.CompetitionId.HasValue
                ? competitions.GetValueOrDefault(item.CompetitionId.Value)?.Name : null,
            item.ParticipantId,
            participantName = participants.GetValueOrDefault(item.ParticipantId)?.Name,
            type = item.Type.ToString(),
            status = item.Status.ToString(),
            item.CreatedAt, item.UpdatedAt, item.RegisteredAt,
            item.ReviewNotes, item.CancellationReason,
            item.FormSubmissionId,
            formStatus = item.FormSubmissionId.HasValue
                ? submissions.GetValueOrDefault(item.FormSubmissionId.Value)?.Status.ToString() : null,
            formRevision = item.FormSubmissionId.HasValue
                ? submissions.GetValueOrDefault(item.FormSubmissionId.Value)?.Revision : null
        }));
    }

    [HttpGet("registrations/{id:guid}")]
    public async Task<IActionResult> GetRegistration(Guid id, CancellationToken cancellationToken)
    {
        var identity = Identity();
        if (identity.Result is not null) return identity.Result;
        var registration = await OwnedRegistration(id, identity.UserId, cancellationToken);
        if (registration is null) return NotFound();
        var participant = await _database.Participants.AsNoTracking()
            .SingleAsync(item => item.Id == registration.ParticipantId, cancellationToken);
        var competition = registration.CompetitionId.HasValue
            ? await _database.Competitions.AsNoTracking().SingleAsync(
                item => item.Id == registration.CompetitionId.Value, cancellationToken) : null;
        var submission = registration.FormSubmissionId.HasValue
            ? await _database.RegistrationFormSubmissions.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == registration.FormSubmissionId.Value, cancellationToken) : null;
        var form = submission is not null
            ? await _database.RegistrationForms.AsNoTracking().SingleAsync(
                item => item.Id == submission.FormId, cancellationToken) : null;
        return Ok(new
        {
            registration.Id, registration.CompetitionId,
            competitionName = competition?.Name,
            registration.ParticipantId, participantName = participant.Name,
            type = registration.Type.ToString(), status = registration.Status.ToString(),
            registration.CreatedAt, registration.UpdatedAt, registration.RegisteredAt,
            registration.ReviewNotes, registration.CancellationReason,
            submission = submission is null ? null : new
            {
                submission.Id, submission.FormId, formName = form?.Name,
                status = submission.Status.ToString(), submission.FormData,
                submission.FormVersion, submission.FormSchemaSnapshot,
                submission.Revision, submission.ReviewNotes
            }
        });
    }

    [HttpPost("registrations")]
    public async Task<IActionResult> CreateRegistration(
        [FromBody] CreateApplicantRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var identity = Identity();
        if (identity.Result is not null) return identity.Result;
        var competition = await _database.Competitions.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.CompetitionId && item.TenantId == identity.TenantId,
            cancellationToken);
        if (competition is null) return NotFound(new { error = "Competition not found in your tenant." });
        var participant = await _database.Participants.SingleOrDefaultAsync(
            item => item.Id == request.ParticipantId && item.TenantId == identity.TenantId
                && item.ApplicantUserId == identity.UserId,
            cancellationToken);
        if (participant is null) return BadRequest(new { error = "Select a participant profile owned by your account." });
        var form = await _database.RegistrationForms.SingleOrDefaultAsync(
            item => item.Id == request.FormId && item.TenantId == identity.TenantId
                && item.IsActive && (item.CompetitionId == null || item.CompetitionId == competition.Id),
            cancellationToken);
        if (form is null) return BadRequest(new { error = "The selected registration form is unavailable." });
        if (await _database.Registrations.AnyAsync(item =>
                item.CompetitionId == competition.Id && item.ParticipantId == participant.Id,
                cancellationToken))
            return Conflict(new { error = "This participant already has a registration for the competition." });

        var type = participant.Type == ParticipantType.Athlete
            ? RegistrationType.Individual : RegistrationType.Team;
        var submission = new RegistrationFormSubmission(
            Guid.NewGuid(), form.Id, participant.Id, form.Version, form.FormSchema);
        submission.AssignApplicant(identity.UserId);
        var registration = Registration.CreateForCompetition(
            Guid.NewGuid(), competition.Id, participant.Id, type);
        registration.AssignApplicant(identity.UserId);
        registration.LinkFormSubmission(submission.Id);
        submission.LinkRegistration(registration.Id);
        _database.RegistrationFormSubmissions.Add(submission);
        _database.Registrations.Add(registration);
        await _database.SaveChangesAsync(cancellationToken);
        await AuditRegistration(registration, identity, "ApplicantRegistrationDraftCreated",
            "Applicant created a registration draft", cancellationToken);
        return CreatedAtAction(nameof(GetRegistration), new { id = registration.Id }, new
        {
            registration.Id, registration.FormSubmissionId,
            status = registration.Status.ToString(), formStatus = submission.Status.ToString()
        });
    }

    [HttpPut("registrations/{id:guid}/draft")]
    public async Task<IActionResult> SaveDraft(
        Guid id,
        [FromBody] SaveApplicantDraftRequest request,
        CancellationToken cancellationToken)
    {
        var identity = Identity();
        if (identity.Result is not null) return identity.Result;
        var registration = await OwnedRegistration(id, identity.UserId, cancellationToken);
        if (registration?.FormSubmissionId is null) return NotFound();
        var submission = await _database.RegistrationFormSubmissions.SingleAsync(
            item => item.Id == registration.FormSubmissionId.Value, cancellationToken);
        if (submission.Status is not (RegistrationFormSubmissionStatus.Draft
            or RegistrationFormSubmissionStatus.ChangesRequested))
            return BadRequest(new { error = "Only draft or changes-requested forms can be edited." });
        submission.UpdateFormData(JsonSerializer.Serialize(request.FormData));
        await _database.SaveChangesAsync(cancellationToken);
        await AuditRegistration(registration, identity, "ApplicantRegistrationDraftSaved",
            $"Form revision {submission.Revision} saved", cancellationToken);
        return Ok(new { registration.Id, submission.Revision, formStatus = submission.Status.ToString() });
    }

    [HttpPost("registrations/{id:guid}/submit")]
    public async Task<IActionResult> Submit(
        Guid id,
        CancellationToken cancellationToken)
    {
        var identity = Identity();
        if (identity.Result is not null) return identity.Result;
        var registration = await OwnedRegistration(id, identity.UserId, cancellationToken);
        if (registration?.FormSubmissionId is null) return NotFound();
        var submission = await _database.RegistrationFormSubmissions.SingleAsync(
            item => item.Id == registration.FormSubmissionId.Value, cancellationToken);
        if (submission.Status is not (RegistrationFormSubmissionStatus.Draft
            or RegistrationFormSubmissionStatus.ChangesRequested))
            return BadRequest(new { error = "This form has already been submitted." });
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(submission.FormData) ?? new();
        var validation = RegistrationFormSchema.Validate(submission.FormSchemaSnapshot, data);
        if (!validation.IsValid) return BadRequest(new { error = "Complete all required fields.", errors = validation.Errors });
        submission.Submit();
        if (registration.Status == RegistrationStatus.Draft)
            registration.Submit(identity.UserId);
        await _database.SaveChangesAsync(cancellationToken);
        await AuditRegistration(registration, identity, "ApplicantRegistrationSubmitted",
            "Applicant submitted registration for review", cancellationToken);
        return Ok(new { registration.Id, status = registration.Status.ToString(), formStatus = submission.Status.ToString() });
    }

    [HttpPost("registrations/{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(
        Guid id,
        [FromBody] ApplicantWithdrawalRequest request,
        CancellationToken cancellationToken)
    {
        var identity = Identity();
        if (identity.Result is not null) return identity.Result;
        var registration = await OwnedRegistration(id, identity.UserId, cancellationToken);
        if (registration is null) return NotFound();
        try { registration.Withdraw(request.Reason); }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        { return BadRequest(new { error = exception.Message }); }
        await _database.SaveChangesAsync(cancellationToken);
        await AuditRegistration(registration, identity, "ApplicantRegistrationWithdrawn",
            request.Reason, cancellationToken);
        return Ok(new { registration.Id, status = registration.Status.ToString() });
    }

    [HttpPost("registrations/{id:guid}/files/{fieldId}")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> UploadFile(
        Guid id, string fieldId, IFormFile file, CancellationToken cancellationToken)
    {
        var identity = Identity();
        if (identity.Result is not null) return identity.Result;
        var registration = await OwnedRegistration(id, identity.UserId, cancellationToken);
        if (registration?.FormSubmissionId is null) return NotFound();
        var submission = await _database.RegistrationFormSubmissions.AsNoTracking().SingleAsync(
            item => item.Id == registration.FormSubmissionId.Value, cancellationToken);
        if (submission.Status is not (RegistrationFormSubmissionStatus.Draft
            or RegistrationFormSubmissionStatus.ChangesRequested))
            return BadRequest(new { error = "This registration can no longer accept uploads." });
        var field = (JsonSerializer.Deserialize<List<RegistrationFormField>>(
            submission.FormSchemaSnapshot) ?? []).SingleOrDefault(item =>
                item.Id.Equals(fieldId, StringComparison.OrdinalIgnoreCase));
        if (field is null || !field.FieldType.Equals("File", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "The selected field is not a file field." });
        if (file.Length <= 0 || file.Length > (field.MaxFileSizeBytes ?? 5 * 1024 * 1024))
            return BadRequest(new { error = "The uploaded file exceeds the configured size limit." });
        var contentType = file.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (field.AllowedContentTypes?.Contains(contentType, StringComparer.OrdinalIgnoreCase) != true)
            return BadRequest(new { error = "The uploaded file type is not allowed." });
        var extension = contentType switch
        {
            "application/pdf" => ".pdf", "image/jpeg" => ".jpg",
            "image/png" => ".png", "image/webp" => ".webp", _ => ".bin"
        };
        var storedName = $"applicant-registration-{registration.Id:N}-{field.Id}-{Guid.NewGuid():N}{extension}";
        await using var stream = file.OpenReadStream();
        await _assetStorage.StoreAsync(identity.TenantId, storedName, stream, file.Length, contentType, cancellationToken);
        var url = $"{Request.Scheme}://{Request.Host}/api/applicant/files/{identity.TenantId:D}/{storedName}";
        return Ok(new { url, fileName = Path.GetFileName(file.FileName), contentType, size = file.Length });
    }

    [HttpGet("files/{tenantId:guid}/{fileName}")]
    public async Task<IActionResult> DownloadFile(
        Guid tenantId, string fileName, CancellationToken cancellationToken)
    {
        var identity = Identity();
        if (identity.Result is not null) return identity.Result;
        if (tenantId != identity.TenantId || !fileName.StartsWith("applicant-registration-", StringComparison.Ordinal))
            return Forbid();
        var ownedRegistrationIds = await _database.Registrations.AsNoTracking()
            .Where(item => item.ApplicantUserId == identity.UserId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (!ownedRegistrationIds.Any(id => fileName.StartsWith(
                $"applicant-registration-{id:N}-", StringComparison.Ordinal)))
            return Forbid();
        var asset = await _assetStorage.GetAsync(tenantId, fileName, cancellationToken);
        return asset is null ? NotFound() : File(asset.Content, asset.ContentType);
    }

    private async Task<Registration?> OwnedRegistration(
        Guid id, Guid userId, CancellationToken cancellationToken) =>
        await _database.Registrations.SingleOrDefaultAsync(
            item => item.Id == id && item.ApplicantUserId == userId, cancellationToken);

    private (Guid UserId, Guid TenantId, IActionResult? Result) Identity()
    {
        var userId = User.UserId();
        var tenantId = User.TenantId();
        if (!userId.HasValue) return (Guid.Empty, Guid.Empty, Unauthorized());
        if (!tenantId.HasValue) return (userId.Value, Guid.Empty,
            BadRequest(new { error = "Select or create an account within a tenant before registering." }));
        return (userId.Value, tenantId.Value, null);
    }

    private Task AuditRegistration(
        Registration registration,
        (Guid UserId, Guid TenantId, IActionResult? Result) identity,
        string action,
        string reason,
        CancellationToken cancellationToken) =>
        _audit.RecordAsync(action, "Registration", registration.Id.ToString(),
            identity.TenantId, identity.UserId, null,
            JsonSerializer.Serialize(new
            {
                registration.CompetitionId, registration.ParticipantId,
                registration.FormSubmissionId, status = registration.Status.ToString()
            }), reason, cancellationToken);
}

public sealed record CreateApplicantParticipantRequest(string Name, string ShortName, string Type);
public sealed record CreateApplicantRegistrationRequest(Guid CompetitionId, Guid ParticipantId, Guid FormId);
public sealed record SaveApplicantDraftRequest(Dictionary<string, object> FormData);
public sealed record ApplicantWithdrawalRequest(string Reason);
