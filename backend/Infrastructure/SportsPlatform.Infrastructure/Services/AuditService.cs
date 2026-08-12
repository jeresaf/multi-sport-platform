using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Modules.Audit.Entities;
using SportsPlatform.Infrastructure.Persistence;

namespace SportsPlatform.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly SportsPlatformDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(
        SportsPlatformDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task RecordAsync(
        string action,
        string entityType,
        string entityId,
        Guid? tenantId = null,
        Guid? userId = null,
        string? originalValue = null,
        string? newValue = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditEntry(
            Guid.NewGuid(),
            tenantId,
            userId,
            action,
            entityType,
            entityId,
            NormalizeJson(originalValue),
            NormalizeJson(newValue),
            reason);

        var httpContext = _httpContextAccessor.HttpContext;
        entry.SetRequestContext(
            httpContext?.Connection.RemoteIpAddress?.ToString(),
            httpContext?.Request.Headers.UserAgent.ToString());

        _context.AuditEntries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizeJson(string? value)
    {
        if (value is null)
            return null;

        try
        {
            using var _ = JsonDocument.Parse(value);
            return value;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(value);
        }
    }

    public async Task<IReadOnlyList<AuditEntry>> GetByTenantAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditEntries
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetByEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditEntries
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }
}
