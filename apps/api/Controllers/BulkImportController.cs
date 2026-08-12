using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Api.Services;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Participants.Services;

namespace SportsPlatform.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public sealed class BulkImportController(
    CsvImportParser parser, CsvUploadValidator uploadValidator,
    SportsPlatformDbContext database, IBulkImportQueue queue,
    BulkImportJobProcessor processor, IAuditService audit) : ControllerBase
{
    private const int BackgroundThreshold = 100;

    [HttpPost("preview/athletes"), EnableRateLimiting("uploads"), RequirePermission("participants.create")]
    [RequestSizeLimit(CsvUploadValidator.MaximumBytes)]
    public async Task<IActionResult> PreviewAthletes(IFormFile file, [FromQuery] Guid? tenantId, CancellationToken ct)
    {
        var scope = ResolveTenant(tenantId); if (!scope.HasValue) return Forbid();
        var stream = await Validate(file, ["Name"], ct); if (stream is null) return ValidationProblem();
        using (stream) return Ok(await PreviewAthletes(parser.ParseAthletesWithErrors(stream), scope.Value, ct));
    }

    [HttpPost("preview/teams"), EnableRateLimiting("uploads"), RequirePermission("participants.create")]
    [RequestSizeLimit(CsvUploadValidator.MaximumBytes)]
    public async Task<IActionResult> PreviewTeams(IFormFile file, [FromQuery] Guid? tenantId, CancellationToken ct)
    {
        var scope = ResolveTenant(tenantId); if (!scope.HasValue) return Forbid();
        var stream = await Validate(file, ["Name"], ct); if (stream is null) return ValidationProblem();
        using (stream) return Ok(await PreviewTeams(parser.ParseTeamsWithErrors(stream), scope.Value, ct));
    }

    [HttpPost("preview/rosters/{rosterId:guid}/entries"), EnableRateLimiting("uploads"), RequirePermission("participants.create")]
    [RequestSizeLimit(CsvUploadValidator.MaximumBytes)]
    public async Task<IActionResult> PreviewRoster(Guid rosterId, IFormFile file, [FromQuery] Guid? tenantId, CancellationToken ct)
    {
        var scope = ResolveTenant(tenantId); if (!scope.HasValue) return Forbid();
        var roster = await database.Set<TeamRoster>().AsNoTracking().Include(item => item.Entries)
            .SingleOrDefaultAsync(item => item.Id == rosterId && item.TenantId == scope, ct);
        if (roster is null) return NotFound(new { error = "Roster not found." });
        var stream = await Validate(file, ["AthleteName"], ct); if (stream is null) return ValidationProblem();
        using (stream) return Ok(await PreviewRoster(parser.ParseRosterEntriesWithErrors(stream), roster, scope.Value, ct));
    }

    [HttpPost("athletes"), EnableRateLimiting("uploads"), RequirePermission("participants.create")]
    [RequestSizeLimit(CsvUploadValidator.MaximumBytes)]
    public async Task<IActionResult> ImportAthletes(IFormFile file, [FromQuery] Guid? tenantId,
        [FromQuery] DuplicateHandling duplicateHandling = DuplicateHandling.Skip, CancellationToken ct = default)
    {
        var scope = ResolveTenant(tenantId); if (!scope.HasValue) return Forbid();
        var stream = await Validate(file, ["Name"], ct); if (stream is null) return ValidationProblem();
        using (stream)
        {
            var parsed = parser.ParseAthletesWithErrors(stream);
            return await CreateJob("athletes", file.FileName, scope.Value, null, parsed.Records, parsed.Errors, duplicateHandling, ct);
        }
    }

    [HttpPost("teams"), EnableRateLimiting("uploads"), RequirePermission("participants.create")]
    [RequestSizeLimit(CsvUploadValidator.MaximumBytes)]
    public async Task<IActionResult> ImportTeams(IFormFile file, [FromQuery] Guid? tenantId,
        [FromQuery] DuplicateHandling duplicateHandling = DuplicateHandling.Skip, CancellationToken ct = default)
    {
        var scope = ResolveTenant(tenantId); if (!scope.HasValue) return Forbid();
        var stream = await Validate(file, ["Name"], ct); if (stream is null) return ValidationProblem();
        using (stream)
        {
            var parsed = parser.ParseTeamsWithErrors(stream);
            return await CreateJob("teams", file.FileName, scope.Value, null, parsed.Records, parsed.Errors, duplicateHandling, ct);
        }
    }

    [HttpPost("rosters/{rosterId:guid}/entries"), EnableRateLimiting("uploads"), RequirePermission("participants.create")]
    [RequestSizeLimit(CsvUploadValidator.MaximumBytes)]
    public async Task<IActionResult> ImportRoster(Guid rosterId, IFormFile file, [FromQuery] Guid? tenantId,
        [FromQuery] DuplicateHandling duplicateHandling = DuplicateHandling.Skip, CancellationToken ct = default)
    {
        var scope = ResolveTenant(tenantId); if (!scope.HasValue) return Forbid();
        if (!await database.Set<TeamRoster>().AnyAsync(item => item.Id == rosterId && item.TenantId == scope, ct)) return NotFound();
        var stream = await Validate(file, ["AthleteName"], ct); if (stream is null) return ValidationProblem();
        using (stream)
        {
            var parsed = parser.ParseRosterEntriesWithErrors(stream);
            return await CreateJob("roster-entries", file.FileName, scope.Value, rosterId, parsed.Records, parsed.Errors, duplicateHandling, ct);
        }
    }

    [HttpGet("history"), RequirePermission("participants.read")]
    public async Task<IActionResult> History([FromQuery] Guid? tenantId, CancellationToken ct)
    {
        var scope = ResolveTenant(tenantId); if (!scope.HasValue) return Forbid();
        return Ok(await database.BulkImportJobs.AsNoTracking().Where(item => item.TenantId == scope)
            .OrderByDescending(item => item.CreatedAt).Take(100).Select(item => Summary(item)).ToArrayAsync(ct));
    }

    [HttpGet("{id:guid}"), RequirePermission("participants.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var job = await database.BulkImportJobs.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        return job is null ? NotFound() : !User.CanAccessTenant(job.TenantId) ? Forbid() : Ok(Details(job));
    }

    [HttpGet("{id:guid}/report"), RequirePermission("participants.read")]
    public async Task<IActionResult> Report(Guid id, CancellationToken ct)
    {
        var job = await database.BulkImportJobs.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (job is null) return NotFound(); if (!User.CanAccessTenant(job.TenantId)) return Forbid();
        var csv = new StringBuilder("Row,Status,Message\r\n");
        csv.Append("0,Summary,").Append(Csv($"Imported={job.ImportedRows}; Duplicates={job.DuplicateRows}; Failed={job.FailedRows}; Total={job.TotalRows}")).Append("\r\n");
        foreach (var error in job.GetErrors()) csv.Append(error.Row).Append(",Error,").Append(Csv(error.Message)).Append("\r\n");
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"{job.Kind}-import-{job.Id:N}.csv");
    }

    [HttpGet("templates/athletes"), RequirePermission("participants.read")]
    public IActionResult AthleteTemplate() => Template("athletes_template.csv",
        "Name,ShortName,DateOfBirth,Gender,Nationality,Email,Phone,EmergencyContact,MedicalClearance\r\nJohn Smith,JS,1995-03-15,Male,Kenya,john@example.com,+254700000000,Jane Smith,true\r\n");
    [HttpGet("templates/teams"), RequirePermission("participants.read")]
    public IActionResult TeamTemplate() => Template("teams_template.csv",
        "Name,ShortName,HomeVenue,Coach,Manager\r\nNairobi Lions,NL,City Stadium,A Coach,A Manager\r\n");
    [HttpGet("templates/roster-entries"), RequirePermission("participants.read")]
    public IActionResult RosterTemplate() => Template("roster_entries_template.csv",
        "AthleteName,JerseyNumber,Position\r\nJohn Smith,10,Forward\r\n");

    private async Task<IActionResult> CreateJob<T>(string kind, string fileName, Guid tenantId, Guid? rosterId,
        IReadOnlyList<T> records, IReadOnlyList<ImportError> errors, DuplicateHandling duplicateHandling, CancellationToken ct)
    {
        if (records.Count + errors.Count == 0) return BadRequest(new { error = "No data rows were found in the file." });
        var actor = User.UserId(); if (!actor.HasValue) return Unauthorized();
        var job = new BulkImportJob(Guid.NewGuid(), tenantId, kind, fileName, duplicateHandling,
            actor.Value, rosterId, records.Count + errors.Count, JsonSerializer.Serialize(records), errors);
        database.BulkImportJobs.Add(job); await database.SaveChangesAsync(ct);
        await audit.RecordAsync("BulkImportQueued", "BulkImportJob", job.Id.ToString(), tenantId, actor,
            null, JsonSerializer.Serialize(new { kind, job.TotalRows, duplicateHandling }), "Bulk import accepted", ct);
        if (job.TotalRows >= BackgroundThreshold)
        {
            await queue.EnqueueAsync(job.Id, ct);
            return AcceptedAtAction(nameof(Get), new { id = job.Id }, Summary(job));
        }
        await processor.ProcessAsync(job.Id, ct);
        var completed = await database.BulkImportJobs.AsNoTracking().SingleAsync(item => item.Id == job.Id, ct);
        return Ok(Details(completed));
    }

    private async Task<ImportPreview> PreviewAthletes(CsvParseResult<AthleteImportDto> parsed, Guid tenantId, CancellationToken ct)
    {
        var existing = (await database.Participants.AsNoTracking().Where(item => item.TenantId == tenantId)
            .Select(item => item.Name).ToArrayAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Preview(parsed.Records, parsed.Errors, item => item.RowNumber, item => item.Name, item =>
        {
            var athlete = new Athlete(Guid.NewGuid(), tenantId, item.Name, item.ShortName ?? Short(item.Name));
            athlete.UpdatePersonalInfo(item.DateOfBirth, item.Gender, item.Nationality);
            athlete.UpdateContact(item.Email, item.Phone, item.EmergencyContact);
        }, existing);
    }

    private async Task<ImportPreview> PreviewTeams(CsvParseResult<TeamImportDto> parsed, Guid tenantId, CancellationToken ct)
    {
        var existing = (await database.Participants.AsNoTracking().Where(item => item.TenantId == tenantId)
            .Select(item => item.Name).ToArrayAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Preview(parsed.Records, parsed.Errors, item => item.RowNumber, item => item.Name, item =>
        {
            var team = new Team(Guid.NewGuid(), tenantId, item.Name, item.ShortName ?? Short(item.Name));
            team.UpdateDetails(item.HomeVenue, item.Coach, item.Manager);
        }, existing);
    }

    private async Task<ImportPreview> PreviewRoster(CsvParseResult<RosterEntryImportDto> parsed, TeamRoster roster, Guid tenantId, CancellationToken ct)
    {
        var athletes = await database.Participants.OfType<Athlete>().AsNoTracking().Where(item => item.TenantId == tenantId)
            .ToDictionaryAsync(item => item.Name, StringComparer.OrdinalIgnoreCase, ct);
        var rows = parsed.Errors.Select(item => new ImportPreviewRow(item.Row, "", "Error", item.Message)).ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in parsed.Records)
        {
            if (!athletes.TryGetValue(item.AthleteName, out var athlete)) rows.Add(new(item.RowNumber, item.AthleteName, "Error", "Athlete not found."));
            else if (!seen.Add(item.AthleteName) || roster.Entries.Any(entry => entry.IsActive && entry.AthleteId == athlete.Id)) rows.Add(new(item.RowNumber, item.AthleteName, "Duplicate", "Athlete is already present."));
            else if (!string.IsNullOrWhiteSpace(item.JerseyNumber) && roster.Entries.Any(entry => entry.IsActive && entry.JerseyNumber == item.JerseyNumber)) rows.Add(new(item.RowNumber, item.AthleteName, "Error", $"Squad number {item.JerseyNumber} is already assigned."));
            else rows.Add(new(item.RowNumber, item.AthleteName, "Valid", null));
        }
        return ImportPreview.From(rows);
    }

    private static ImportPreview Preview<T>(IReadOnlyList<T> records, IReadOnlyList<ImportError> errors,
        Func<T, int> row, Func<T, string> name, Action<T> validate, HashSet<string> existing)
    {
        var output = errors.Select(item => new ImportPreviewRow(item.Row, "", "Error", item.Message)).ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in records)
        {
            try
            {
                if (!seen.Add(name(item)) || existing.Contains(name(item))) output.Add(new(row(item), name(item), "Duplicate", "A participant with this name already exists."));
                else { validate(item); output.Add(new(row(item), name(item), "Valid", null)); }
            }
            catch (Exception exception) { output.Add(new(row(item), name(item), "Error", exception.Message)); }
        }
        return ImportPreview.From(output);
    }

    private async Task<MemoryStream?> Validate(IFormFile file, string[] headers, CancellationToken ct)
    {
        try { return await uploadValidator.ValidateAsync(file, headers, ct); }
        catch (CsvUploadValidationException exception) { ModelState.AddModelError("file", exception.Message); return null; }
    }
    private Guid? ResolveTenant(Guid? requested) { var value = requested ?? User.TenantId(); return value.HasValue && User.CanAccessTenant(value.Value) ? value : null; }
    private static object Summary(BulkImportJob item) => new { item.Id, item.Kind, item.FileName, status = item.Status.ToString(), duplicateHandling = item.DuplicateHandling.ToString(), item.TotalRows, item.ImportedRows, item.DuplicateRows, item.FailedRows, item.CreatedAt, item.StartedAt, item.CompletedAt };
    private static object Details(BulkImportJob item) => new { summary = Summary(item), errors = item.GetErrors() };
    private static string Short(string name) => name[..Math.Min(10, name.Length)];
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private FileContentResult Template(string name, string content) => File(Encoding.UTF8.GetBytes(content), "text/csv", name);
}

public sealed record ImportPreviewRow(int Row, string Name, string Status, string? Message);
public sealed record ImportPreview(int Total, int Valid, int Duplicates, int Invalid, IReadOnlyList<ImportPreviewRow> Rows)
{
    public static ImportPreview From(IReadOnlyList<ImportPreviewRow> rows) => new(rows.Count,
        rows.Count(item => item.Status == "Valid"), rows.Count(item => item.Status == "Duplicate"),
        rows.Count(item => item.Status == "Error"), rows.OrderBy(item => item.Row).ToArray());
}
