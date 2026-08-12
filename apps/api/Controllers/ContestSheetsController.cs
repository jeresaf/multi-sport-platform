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
[Route("api/contest-sheets")]
[ApiVersion("1.0")]
[Authorize]
public sealed class ContestSheetsController : ControllerBase
{
    private readonly SportsPlatformDbContext _database;
    private readonly IReportGenerationQueue _queue;

    public ContestSheetsController(
        SportsPlatformDbContext database,
        IReportGenerationQueue queue)
    {
        _database = database;
        _queue = queue;
    }

    [HttpGet]
    [RequirePermission("reports.read")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? contestId,
        CancellationToken cancellationToken)
    {
        var resolvedTenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (resolvedTenant.Error is not null) return resolvedTenant.Error;
        var reports = await _database.Reports.AsNoTracking()
            .Where(item => item.TenantId == resolvedTenant.TenantId
                && item.Type == ReportType.ContestSheet)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        var views = await BuildViewsAsync(reports, cancellationToken);
        if (contestId.HasValue)
            views = views.Where(item => item.ContestId == contestId.Value).ToArray();
        return Ok(new { items = views, total = views.Length });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("reports.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var report = await _database.Reports.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id
                && item.Type == ReportType.ContestSheet, cancellationToken);
        if (report is null) return NotFound();
        if (!User.CanAccessTenant(report.TenantId)) return Forbid();
        var view = (await BuildViewsAsync(new[] { report }, cancellationToken)).Single();
        return Ok(view);
    }

    [HttpPost]
    [RequirePermission("reports.export")]
    public async Task<IActionResult> Create(
        [FromBody] CreateContestSheetRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedTenant = await ResolveTenantAsync(request.TenantId, cancellationToken);
        if (resolvedTenant.Error is not null) return resolvedTenant.Error;
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        var contest = await _database.Contests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.ContestId
                && item.TenantId == resolvedTenant.TenantId, cancellationToken);
        if (contest is null)
            return BadRequest(new { error = "The contest is unavailable for this tenant." });
        var name = string.IsNullOrWhiteSpace(request.Name)
            ? $"{contest.Name ?? $"Contest {contest.SortOrder}"} contest sheet"
            : request.Name.Trim();
        if (name.Length > 200)
            return BadRequest(new { error = "The archive name must be 200 characters or fewer." });

        var report = new Report(
            Guid.NewGuid(), resolvedTenant.TenantId!.Value, userId.Value,
            name, ReportType.ContestSheet);
        report.SetFormat(ReportFormat.Pdf);
        report.SetDescription("Sport-aware branded printable contest-sheet archive.");
        report.SetParameters(JsonSerializer.Serialize(
            new ReportParameters(null, null, null, contest.Id)));
        _database.Reports.Add(report);
        await _database.SaveChangesAsync(cancellationToken);
        await _queue.EnqueueAsync(report.Id, cancellationToken);
        var view = (await BuildViewsAsync(new[] { report }, cancellationToken)).Single();
        return AcceptedAtAction(nameof(Get), new { id = report.Id }, view);
    }

    [HttpPost("{id:guid}/retry")]
    [RequirePermission("reports.export")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        var report = await _database.Reports.SingleOrDefaultAsync(item => item.Id == id
            && item.Type == ReportType.ContestSheet, cancellationToken);
        if (report is null) return NotFound();
        if (!User.CanAccessTenant(report.TenantId)) return Forbid();
        if (report.Status is ReportStatus.Pending or ReportStatus.Processing)
            return Conflict(new { error = "The contest sheet is already being generated." });
        report.ResetForGeneration();
        await _database.SaveChangesAsync(cancellationToken);
        await _queue.EnqueueAsync(report.Id, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { id },
            (await BuildViewsAsync(new[] { report }, cancellationToken)).Single());
    }

    [HttpGet("{id:guid}/download")]
    [RequirePermission("reports.export")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var report = await _database.Reports.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id
                && item.Type == ReportType.ContestSheet, cancellationToken);
        if (report is null) return NotFound();
        if (!User.CanAccessTenant(report.TenantId)) return Forbid();
        if (report.Status != ReportStatus.Completed || report.GeneratedContent is null)
            return Conflict(new { error = "The contest sheet is not ready for download." });
        return File(report.GeneratedContent, "application/pdf",
            report.GeneratedFileName ?? $"contest-sheet-{report.Id}.pdf");
    }

    private async Task<ContestSheetView[]> BuildViewsAsync(
        IReadOnlyCollection<Report> reports,
        CancellationToken cancellationToken)
    {
        var metadata = reports.Select(report =>
        {
            var parameters = JsonSerializer.Deserialize<ReportParameters>(report.Parameters);
            return new { Report = report, ContestId = parameters?.ContestId };
        }).Where(item => item.ContestId.HasValue).ToArray();
        var contestIds = metadata.Select(item => item.ContestId!.Value).Distinct().ToArray();
        var contests = await _database.Contests.AsNoTracking()
            .Where(item => contestIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Name, item.SortOrder, item.SportId })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var sportIds = contests.Values.Select(item => item.SportId).Distinct().ToArray();
        var sports = await _database.Sports.AsNoTracking()
            .Where(item => sportIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        return metadata.Select(item =>
        {
            contests.TryGetValue(item.ContestId!.Value, out var contest);
            var sport = contest is null ? null : sports.GetValueOrDefault(contest.SportId);
            return new ContestSheetView(
                item.Report.Id,
                item.Report.TenantId,
                item.ContestId.Value,
                contest?.Name ?? (contest is null ? "Archived contest" : $"Contest {contest.SortOrder}"),
                sport?.Code ?? "unknown",
                sport?.Name ?? "Unknown",
                sport is null ? "Contest sheet" : ReportDocumentBuilder.ContestSheetTemplate(sport.Code),
                item.Report.Name,
                item.Report.Status.ToString(),
                item.Report.CreatedBy,
                item.Report.CreatedAt,
                item.Report.GeneratedAt,
                item.Report.GeneratedFileName,
                item.Report.ErrorMessage);
        }).ToArray();
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

    private sealed record ContestSheetView(
        Guid Id,
        Guid TenantId,
        Guid ContestId,
        string ContestName,
        string SportCode,
        string SportName,
        string Template,
        string Name,
        string Status,
        Guid CreatedBy,
        DateTime CreatedAt,
        DateTime? GeneratedAt,
        string? GeneratedFileName,
        string? ErrorMessage);
}

public sealed record CreateContestSheetRequest(Guid? TenantId, Guid ContestId, string? Name);
