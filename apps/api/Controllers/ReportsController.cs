using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Api.Services;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Reporting.Entities;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/reports")]
[ApiVersion("1.0")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private static readonly ReportType[] AvailableTypes =
    {
        ReportType.RegistrationList,
        ReportType.TeamRoster,
        ReportType.OfficialAssignment,
        ReportType.MatchSheet,
        ReportType.Standings,
        ReportType.CompetitionSummary,
        ReportType.Discipline,
        ReportType.AthletePerformance,
        ReportType.AuditHistory,
        ReportType.FixtureSchedule,
        ReportType.Results
    };
    private static readonly ReportFormat[] AvailableFormats =
        { ReportFormat.Pdf, ReportFormat.Csv, ReportFormat.Excel, ReportFormat.Html };
    private readonly SportsPlatformDbContext _database;
    private readonly IReportGenerationQueue _queue;

    public ReportsController(
        SportsPlatformDbContext database,
        IReportGenerationQueue queue)
    {
        _database = database;
        _queue = queue;
    }

    [HttpGet("catalog")]
    [RequirePermission("reports.read")]
    public IActionResult Catalog() => Ok(new
    {
        reportTypes = AvailableTypes.Select(type => new
        {
            value = type.ToString(),
            label = Label(type),
            available = AvailableTypes.Contains(type)
        }),
        formats = AvailableFormats.Select(format => new
        {
            value = format.ToString(),
            label = format == ReportFormat.Excel ? "Excel (.xlsx)" : format.ToString()
        })
    });

    [HttpGet]
    [RequirePermission("reports.read")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var resolvedTenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (resolvedTenant.Error is not null) return resolvedTenant.Error;
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _database.Reports.AsNoTracking()
            .Where(item => item.TenantId == resolvedTenant.TenantId
                && item.Type != ReportType.ContestSheet
                && item.Type != ReportType.TenantDataExport)
            .OrderByDescending(item => item.CreatedAt);
        var total = await query.CountAsync(cancellationToken);
        var metadata = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new ReportMetadata(
                item.Id, item.TenantId, item.Name, item.Type,
                item.Format, item.Status, item.CreatedBy,
                item.CreatedAt, item.GeneratedAt, item.GeneratedUrl,
                item.GeneratedFileName, item.ErrorMessage))
            .ToArrayAsync(cancellationToken);
        var items = metadata.Select(ToView).ToArray();
        return Ok(new { items, page, pageSize, total });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("reports.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var report = await _database.Reports.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new ReportMetadata(
                item.Id, item.TenantId, item.Name, item.Type,
                item.Format, item.Status, item.CreatedBy,
                item.CreatedAt, item.GeneratedAt, item.GeneratedUrl,
                item.GeneratedFileName, item.ErrorMessage))
            .SingleOrDefaultAsync(cancellationToken);
        if (report is null) return NotFound();
        if (!User.CanAccessTenant(report.TenantId)) return Forbid();
        return Ok(ToView(report));
    }

    [HttpPost]
    [RequirePermission("reports.export")]
    public async Task<IActionResult> Create(
        [FromBody] CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedTenant = await ResolveTenantAsync(request.TenantId, cancellationToken);
        if (resolvedTenant.Error is not null) return resolvedTenant.Error;
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        if (!Enum.TryParse<ReportType>(request.Type, true, out var type)
            || !AvailableTypes.Contains(type))
            return BadRequest(new { error = "Select an available report type." });
        if (!Enum.TryParse<ReportFormat>(request.Format, true, out var format)
            || !AvailableFormats.Contains(format))
            return BadRequest(new { error = "Select PDF, CSV, Excel, or HTML format." });
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            return BadRequest(new { error = "Report name is required and must be 200 characters or fewer." });
        if (request.FromUtc.HasValue && request.ToUtc.HasValue
            && request.FromUtc.Value > request.ToUtc.Value)
            return BadRequest(new { error = "The start date must not be after the end date." });
        if (request.CompetitionId.HasValue && !await _database.Competitions.AsNoTracking()
                .AnyAsync(item => item.Id == request.CompetitionId.Value
                    && item.TenantId == resolvedTenant.TenantId, cancellationToken))
            return BadRequest(new { error = "The competition is unavailable for this tenant." });

        var report = new Report(
            Guid.NewGuid(),
            resolvedTenant.TenantId!.Value,
            userId.Value,
            request.Name,
            type);
        report.SetFormat(format);
        report.SetParameters(JsonSerializer.Serialize(new ReportParameters(
            request.CompetitionId,
            request.FromUtc?.ToUniversalTime(),
            request.ToUtc?.ToUniversalTime())));
        _database.Reports.Add(report);
        await _database.SaveChangesAsync(cancellationToken);
        await _queue.EnqueueAsync(report.Id, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { id = report.Id }, ToView(report));
    }

    [HttpPost("{id:guid}/retry")]
    [RequirePermission("reports.export")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        var report = await _database.Reports.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (report is null) return NotFound();
        if (!User.CanAccessTenant(report.TenantId)) return Forbid();
        if (report.Status is ReportStatus.Pending or ReportStatus.Processing)
            return Conflict(new { error = "The report is already being generated." });
        report.ResetForGeneration();
        await _database.SaveChangesAsync(cancellationToken);
        await _queue.EnqueueAsync(report.Id, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { id }, ToView(report));
    }

    [HttpGet("{id:guid}/download")]
    [RequirePermission("reports.export")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var report = await _database.Reports.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (report is null) return NotFound();
        if (!User.CanAccessTenant(report.TenantId)) return Forbid();
        if (report.Status != ReportStatus.Completed || report.GeneratedContent is null)
            return Conflict(new { error = "The report is not ready for download." });
        return File(
            report.GeneratedContent,
            report.GeneratedContentType ?? "application/octet-stream",
            report.GeneratedFileName ?? $"report-{report.Id}");
    }

    private async Task<(Guid? TenantId, IActionResult? Error)> ResolveTenantAsync(
        Guid? requestedTenantId,
        CancellationToken cancellationToken)
    {
        var tenantId = requestedTenantId ?? User.TenantId();
        if (!tenantId.HasValue)
            return (null, BadRequest(new { error = "A tenant is required." }));
        if (!User.CanAccessTenant(tenantId.Value)) return (null, Forbid());
        if (!await _database.Tenants.AsNoTracking()
                .AnyAsync(item => item.Id == tenantId.Value, cancellationToken))
            return (null, NotFound(new { error = "Tenant not found." }));
        return (tenantId, null);
    }

    private static ReportView ToView(Report report) => new(
        report.Id, report.TenantId, report.Name, report.Type.ToString(),
        report.Format.ToString(), report.Status.ToString(), report.CreatedBy,
        report.CreatedAt, report.GeneratedAt, report.GeneratedUrl,
        report.GeneratedFileName, report.ErrorMessage);

    private static ReportView ToView(ReportMetadata report) => new(
        report.Id, report.TenantId, report.Name, report.Type.ToString(),
        report.Format.ToString(), report.Status.ToString(), report.CreatedBy,
        report.CreatedAt, report.GeneratedAt, report.GeneratedUrl,
        report.GeneratedFileName, report.ErrorMessage);

    private static string Label(ReportType type) => type switch
    {
        ReportType.RegistrationList => "Registration list",
        ReportType.TeamRoster => "Team roster",
        ReportType.FixtureSchedule => "Fixture schedule",
        ReportType.OfficialAssignment => "Official assignment",
        ReportType.MatchSheet => "Match sheet",
        ReportType.Results => "Results",
        ReportType.Standings => "Standings",
        ReportType.Discipline => "Discipline",
        ReportType.AthletePerformance => "Athlete performance",
        ReportType.CompetitionSummary => "Competition summary",
        ReportType.AuditHistory => "Audit history",
        _ => type.ToString()
    };

    private sealed record ReportView(
        Guid Id,
        Guid TenantId,
        string Name,
        string Type,
        string Format,
        string Status,
        Guid CreatedBy,
        DateTime CreatedAt,
        DateTime? GeneratedAt,
        string? GeneratedUrl,
        string? GeneratedFileName,
        string? ErrorMessage);

    private sealed record ReportMetadata(
        Guid Id,
        Guid TenantId,
        string Name,
        ReportType Type,
        ReportFormat Format,
        ReportStatus Status,
        Guid CreatedBy,
        DateTime CreatedAt,
        DateTime? GeneratedAt,
        string? GeneratedUrl,
        string? GeneratedFileName,
        string? ErrorMessage);
}

public sealed record CreateReportRequest(
    Guid? TenantId,
    string Name,
    string Type,
    string Format,
    Guid? CompetitionId,
    DateTime? FromUtc,
    DateTime? ToUtc);
