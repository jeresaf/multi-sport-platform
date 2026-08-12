using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Api.Services;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit.Entities;
using SportsPlatform.Modules.Reporting.Entities;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/tenant-exports")]
[ApiVersion("1.0")]
[Authorize]
public sealed class TenantExportsController : ControllerBase
{
    private readonly SportsPlatformDbContext _database;
    private readonly IReportGenerationQueue _queue;

    public TenantExportsController(
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
        CancellationToken cancellationToken)
    {
        var resolvedTenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (resolvedTenant.Error is not null) return resolvedTenant.Error;
        var exports = await _database.Reports.AsNoTracking()
            .Where(item => item.TenantId == resolvedTenant.TenantId
                && item.Type == ReportType.TenantDataExport)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        return Ok(new { items = exports.Select(ToView), total = exports.Length });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("reports.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var export = await _database.Reports.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id
                && item.Type == ReportType.TenantDataExport, cancellationToken);
        if (export is null) return NotFound();
        if (!User.CanAccessTenant(export.TenantId)) return Forbid();
        return Ok(ToView(export));
    }

    [HttpPost]
    [RequirePermission("reports.export")]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantExportRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedTenant = await ResolveTenantAsync(request.TenantId, cancellationToken);
        if (resolvedTenant.Error is not null) return resolvedTenant.Error;
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        var tenant = await _database.Tenants.AsNoTracking()
            .SingleAsync(item => item.Id == resolvedTenant.TenantId, cancellationToken);
        var name = string.IsNullOrWhiteSpace(request.Name)
            ? $"{tenant.ShortName} tenant data {DateTime.UtcNow:yyyy-MM-dd-HHmmss}"
            : request.Name.Trim();
        if (name.Length > 200)
            return BadRequest(new { error = "The export name must be 200 characters or fewer." });

        var export = new Report(
            Guid.NewGuid(), resolvedTenant.TenantId!.Value, userId.Value,
            name, ReportType.TenantDataExport);
        export.SetFormat(ReportFormat.Zip);
        export.SetDescription(
            "Complete tenant-owned JSON data archive. Authentication credentials are excluded.");
        export.SetParameters(JsonSerializer.Serialize(new ReportParameters(null, null, null)));
        _database.Reports.Add(export);
        var audit = new AuditEntry(
            Guid.NewGuid(), export.TenantId, userId.Value,
            "TenantDataExportRequested", "Report", export.Id.ToString(),
            null,
            JsonSerializer.Serialize(new { export.Name, format = "zip/json" }),
            "Authorized tenant administrator requested a tenant data export.");
        audit.SetRequestContext(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
        _database.AuditEntries.Add(audit);
        await _database.SaveChangesAsync(cancellationToken);
        await _queue.EnqueueAsync(export.Id, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { id = export.Id }, ToView(export));
    }

    [HttpPost("{id:guid}/retry")]
    [RequirePermission("reports.export")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        var export = await _database.Reports.SingleOrDefaultAsync(item => item.Id == id
            && item.Type == ReportType.TenantDataExport, cancellationToken);
        if (export is null) return NotFound();
        if (!User.CanAccessTenant(export.TenantId)) return Forbid();
        if (export.Status is ReportStatus.Pending or ReportStatus.Processing)
            return Conflict(new { error = "The tenant export is already being generated." });
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        export.ResetForGeneration();
        _database.AuditEntries.Add(new AuditEntry(
            Guid.NewGuid(), export.TenantId, userId.Value,
            "TenantDataExportRetried", "Report", export.Id.ToString(),
            null, null, "Authorized tenant administrator retried the export."));
        await _database.SaveChangesAsync(cancellationToken);
        await _queue.EnqueueAsync(export.Id, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { id }, ToView(export));
    }

    [HttpGet("{id:guid}/download")]
    [RequirePermission("reports.export")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var export = await _database.Reports.SingleOrDefaultAsync(item => item.Id == id
            && item.Type == ReportType.TenantDataExport, cancellationToken);
        if (export is null) return NotFound();
        if (!User.CanAccessTenant(export.TenantId)) return Forbid();
        if (export.Status != ReportStatus.Completed || export.GeneratedContent is null)
            return Conflict(new { error = "The tenant export is not ready for download." });
        var userId = User.UserId();
        if (!userId.HasValue) return Unauthorized();
        var audit = new AuditEntry(
            Guid.NewGuid(), export.TenantId, userId.Value,
            "TenantDataExportDownloaded", "Report", export.Id.ToString(),
            null,
            JsonSerializer.Serialize(new
            {
                export.GeneratedFileName,
                bytes = export.GeneratedContent.Length
            }),
            "Authorized tenant administrator downloaded the tenant data export.");
        audit.SetRequestContext(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
        _database.AuditEntries.Add(audit);
        await _database.SaveChangesAsync(cancellationToken);
        return File(export.GeneratedContent, "application/zip",
            export.GeneratedFileName ?? $"tenant-data-export-{export.Id}.zip");
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

    private static TenantExportView ToView(Report export) => new(
        export.Id, export.TenantId, export.Name, export.Status.ToString(),
        export.CreatedBy, export.CreatedAt, export.GeneratedAt,
        export.GeneratedFileName, export.ErrorMessage);

    private sealed record TenantExportView(
        Guid Id,
        Guid TenantId,
        string Name,
        string Status,
        Guid CreatedBy,
        DateTime CreatedAt,
        DateTime? GeneratedAt,
        string? GeneratedFileName,
        string? ErrorMessage);
}

public sealed record CreateTenantExportRequest(Guid? TenantId, string? Name);
