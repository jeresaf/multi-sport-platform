using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Registrations.Entities;
using SportsPlatform.Modules.Registrations.Services;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Api.Services;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RegistrationFormsController : ControllerBase
{
    private readonly IRegistrationFormService _formService;
    private readonly SportsPlatformDbContext _database;
    private readonly IAuditService _audit;
    private readonly IBrandingAssetStorage _assetStorage;

    public RegistrationFormsController(
        IRegistrationFormService formService,
        SportsPlatformDbContext database,
        IAuditService audit,
        IBrandingAssetStorage assetStorage)
    {
        _formService = formService;
        _database = database;
        _audit = audit;
        _assetStorage = assetStorage;
    }

    #region Form Management

    /// <summary>
    /// Create a new registration form
    /// </summary>
    [HttpPost]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> CreateForm(
        [FromBody] CreateFormRequest request,
        CancellationToken cancellationToken)
    {
        if (!await User.RegistrationFormResourcesShareTenantAsync(
                _database,
                request.TenantId,
                request.CompetitionId,
                cancellationToken))
            return Forbid();

        var form = await _formService.CreateFormAsync(
            request.TenantId,
            request.Name,
            User.UserId()?.ToString() ?? "unknown",
            request.CompetitionId,
            request.Description,
            cancellationToken);

        await AuditFormAsync("RegistrationFormCreated", form,
            new { form.Name, form.CompetitionId, form.Version }, "Registration form created", cancellationToken);
        return CreatedAtAction(nameof(GetForm), new { id = form.Id }, form);
    }

    /// <summary>
    /// Get a registration form by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequirePermission("participants.read")]
    public async Task<IActionResult> GetForm(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationFormAsync(_database, id, cancellationToken))
            return Forbid();

        var form = await _formService.GetFormAsync(id, cancellationToken);
        if (form == null)
            return NotFound();

        return Ok(form);
    }

    [HttpGet("{id:guid}/schema")]
    [RequirePermission("participants.read")]
    public async Task<IActionResult> GetFormSchema(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationFormAsync(_database, id, cancellationToken))
            return Forbid();
        var form = await _formService.GetFormAsync(id, cancellationToken);
        if (form is null) return NotFound();
        return Ok(new
        {
            form.Id, form.TenantId, form.CompetitionId, form.Name, form.Description,
            form.Version, form.IsActive,
            Fields = JsonSerializer.Deserialize<List<RegistrationFormField>>(form.FormSchema) ?? []
        });
    }

    [AllowAnonymous]
    [HttpGet("public/competition/{competitionId:guid}")]
    public async Task<IActionResult> GetPublicCompetitionForms(
        Guid competitionId, CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == competitionId, cancellationToken);
        if (competition is null) return NotFound();
        var forms = (await _formService.GetFormsByCompetitionAsync(competitionId, cancellationToken))
            .Where(form => form.IsActive)
            .Select(form => new
            {
                form.Id, form.Name, form.Description, form.Version,
                Fields = JsonSerializer.Deserialize<List<RegistrationFormField>>(form.FormSchema) ?? []
            });
        Response.Headers.CacheControl = "public, max-age=30, stale-while-revalidate=120";
        return Ok(forms);
    }

    /// <summary>
    /// Get all forms for a tenant
    /// </summary>
    [HttpGet("tenant/{tenantId:guid}")]
    [RequirePermission("participants.read")]
    public async Task<IActionResult> GetFormsByTenant(
        Guid tenantId,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();

        var forms = await _formService.GetFormsByTenantAsync(tenantId, isActive, cancellationToken);
        return Ok(forms);
    }

    /// <summary>
    /// Get all forms for a competition
    /// </summary>
    [HttpGet("competition/{competitionId:guid}")]
    [RequirePermission("participants.read")]
    public async Task<IActionResult> GetFormsByCompetition(
        Guid competitionId,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessCompetitionAsync(_database, competitionId, cancellationToken))
            return Forbid();

        var forms = await _formService.GetFormsByCompetitionAsync(competitionId, cancellationToken);
        return Ok(forms);
    }

    /// <summary>
    /// Update form schema with fields
    /// </summary>
    [HttpPut("{id:guid}/schema")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> UpdateFormSchema(
        Guid id,
        [FromBody] UpdateFormSchemaRequest request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationFormAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var form = await _formService.UpdateFormSchemaAsync(id, request.Fields, cancellationToken);
            await AuditFormAsync("RegistrationFormSchemaUpdated", form,
                new { form.Version, FieldCount = request.Fields.Count }, "Registration form schema updated", cancellationToken);
            return Ok(form);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update form metadata
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> UpdateForm(
        Guid id,
        [FromBody] UpdateFormRequest request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationFormAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var form = await _formService.UpdateFormAsync(id, request.Name, request.Description, cancellationToken);
            return Ok(form);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Activate a form
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> ActivateForm(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationFormAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var form = await _formService.ActivateFormAsync(id, cancellationToken);
            return Ok(form);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deactivate a form
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> DeactivateForm(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationFormAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var form = await _formService.DeactivateFormAsync(id, cancellationToken);
            return Ok(form);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Clone a form
    /// </summary>
    [HttpPost("{id:guid}/clone")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> CloneForm(
        Guid id,
        [FromBody] CloneFormRequest request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationFormAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var form = await _formService.CloneFormAsync(
                id,
                request.NewName,
                User.UserId()?.ToString() ?? "unknown",
                cancellationToken);
            return CreatedAtAction(nameof(GetForm), new { id = form.Id }, form);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Form Submissions

    /// <summary>
    /// Create a new form submission
    /// </summary>
    [HttpPost("submissions")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> CreateSubmission(
        [FromBody] CreateSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        if (!await User.RegistrationSubmissionResourcesShareTenantAsync(
                _database,
                request.FormId,
                request.ParticipantId,
                request.ContestId,
                cancellationToken))
            return Forbid();

        var submission = await _formService.CreateSubmissionAsync(
            request.FormId,
            request.ParticipantId,
            request.ContestId,
            cancellationToken);

        return CreatedAtAction(nameof(GetSubmission), new { id = submission.Id }, submission);
    }

    /// <summary>
    /// Get a submission by ID
    /// </summary>
    [HttpGet("submissions/{id:guid}")]
    [RequirePermission("participants.read")]
    public async Task<IActionResult> GetSubmission(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationSubmissionAsync(_database, id, cancellationToken))
            return Forbid();

        var submission = await _formService.GetSubmissionAsync(id, cancellationToken);
        if (submission == null)
            return NotFound();

        return Ok(submission);
    }

    /// <summary>
    /// Get all submissions for a form
    /// </summary>
    [HttpGet("{formId:guid}/submissions")]
    [RequirePermission("participants.read")]
    public async Task<IActionResult> GetSubmissionsByForm(
        Guid formId,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationFormAsync(_database, formId, cancellationToken))
            return Forbid();

        var submissions = await _formService.GetSubmissionsByFormAsync(formId, cancellationToken);
        return Ok(submissions);
    }

    /// <summary>
    /// Get all submissions for a participant
    /// </summary>
    [HttpGet("submissions/participant/{participantId:guid}")]
    [RequirePermission("participants.read")]
    public async Task<IActionResult> GetSubmissionsByParticipant(
        Guid participantId,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessParticipantAsync(_database, participantId, cancellationToken))
            return Forbid();

        var submissions = await _formService.GetSubmissionsByParticipantAsync(participantId, cancellationToken);
        return Ok(submissions);
    }

    /// <summary>
    /// Update submission form data
    /// </summary>
    [HttpPut("submissions/{id:guid}/data")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> UpdateSubmissionData(
        Guid id,
        [FromBody] UpdateSubmissionDataRequest request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationSubmissionAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var submission = await _formService.UpdateSubmissionDataAsync(id, request.FormData, cancellationToken);
            return Ok(submission);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Submit a form
    /// </summary>
    [HttpPost("submissions/{id:guid}/submit")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> SubmitForm(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationSubmissionAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var submission = await _formService.SubmitFormAsync(id, cancellationToken);
            return Ok(submission);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Start review of a submission
    /// </summary>
    [HttpPost("submissions/{id:guid}/review")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> StartReview(
        Guid id,
        [FromBody] ReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationSubmissionAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var submission = await _formService.StartReviewAsync(
                id, User.UserId() ?? Guid.Empty, cancellationToken);
            return Ok(submission);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Approve a submission
    /// </summary>
    [HttpPost("submissions/{id:guid}/approve")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> ApproveSubmission(
        Guid id,
        [FromBody] ReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationSubmissionAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var submission = await _formService.ApproveSubmissionAsync(
                id, User.UserId() ?? Guid.Empty, cancellationToken);
            return Ok(submission);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Reject a submission
    /// </summary>
    [HttpPost("submissions/{id:guid}/reject")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> RejectSubmission(
        Guid id,
        [FromBody] RejectSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationSubmissionAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var submission = await _formService.RejectSubmissionAsync(
                id, User.UserId() ?? Guid.Empty, request.Notes, cancellationToken);
            return Ok(submission);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Request changes on a submission
    /// </summary>
    [HttpPost("submissions/{id:guid}/request-changes")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> RequestChanges(
        Guid id,
        [FromBody] RejectSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationSubmissionAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var submission = await _formService.RequestChangesAsync(
                id, User.UserId() ?? Guid.Empty, request.Notes, cancellationToken);
            return Ok(submission);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Validate form data
    /// </summary>
    [HttpPost("{formId:guid}/validate")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> ValidateFormData(
        Guid formId,
        [FromBody] ValidateFormDataRequest request,
        CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationFormAsync(_database, formId, cancellationToken))
            return Forbid();

        var (isValid, errors) = await _formService.ValidateSubmissionAsync(formId, request.FormData, cancellationToken);
        return Ok(new { isValid, errors });
    }

    [HttpPost("{formId:guid}/fields/{fieldId}/files")]
    [RequirePermission("participants.create")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> UploadFieldFile(
        Guid formId, string fieldId, IFormFile file, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRegistrationFormAsync(_database, formId, cancellationToken))
            return Forbid();
        var form = await _formService.GetFormAsync(formId, cancellationToken);
        if (form is null) return NotFound();
        var field = (JsonSerializer.Deserialize<List<RegistrationFormField>>(form.FormSchema) ?? [])
            .SingleOrDefault(item => item.Id.Equals(fieldId, StringComparison.OrdinalIgnoreCase));
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
        var fileName = $"registration-{Guid.NewGuid():N}{extension}";
        await using var stream = file.OpenReadStream();
        await _assetStorage.StoreAsync(
            form.TenantId, fileName, stream, file.Length, contentType, cancellationToken);
        var url = $"{Request.Scheme}://{Request.Host}/api/registrationforms/files/{form.TenantId:D}/{fileName}";
        return Ok(new { url, fileName = Path.GetFileName(file.FileName), contentType, size = file.Length });
    }

    [HttpGet("files/{tenantId:guid}/{fileName}")]
    [RequirePermission("participants.read")]
    public async Task<IActionResult> DownloadFieldFile(
        Guid tenantId, string fileName, CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                fileName, "^registration-[0-9a-f]{32}\\.(pdf|jpg|png|webp|bin)$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            return NotFound();
        var asset = await _assetStorage.GetAsync(tenantId, fileName, cancellationToken);
        return asset is null ? NotFound() : File(asset.Content, asset.ContentType);
    }

    #endregion

    private Task AuditFormAsync(
        string action,
        RegistrationForm form,
        object current,
        string reason,
        CancellationToken cancellationToken) =>
        _audit.RecordAsync(action, "RegistrationForm", form.Id.ToString(), form.TenantId,
            User.UserId(), null, JsonSerializer.Serialize(current), reason, cancellationToken);
}

#region Request DTOs

public class CreateFormRequest
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public Guid? CompetitionId { get; set; }
    public string? Description { get; set; }
}

public class UpdateFormSchemaRequest
{
    public List<RegistrationFormField> Fields { get; set; } = new();
}

public class UpdateFormRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public class CloneFormRequest
{
    public string NewName { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

public class CreateSubmissionRequest
{
    public Guid FormId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid? ContestId { get; set; }
}

public class UpdateSubmissionDataRequest
{
    public Dictionary<string, object> FormData { get; set; } = new();
}

public class ReviewRequest
{
    public Guid ReviewerId { get; set; }
}

public class RejectSubmissionRequest
{
    public Guid ReviewerId { get; set; }
    public string? Notes { get; set; }
}

public class ValidateFormDataRequest
{
    public Dictionary<string, object> FormData { get; set; } = new();
}

#endregion
