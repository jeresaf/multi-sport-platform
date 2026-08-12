using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize]
public sealed class AuditController : ControllerBase
{
    private readonly SportsPlatformDbContext _database;

    public AuditController(SportsPlatformDbContext database) => _database = database;

    [HttpGet]
    [RequirePermission("audit.read")]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? userId,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] string? search,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (tenantId.HasValue && !User.CanAccessTenant(tenantId.Value)) return Forbid();
        var effectiveTenantId = tenantId ?? User.TenantId();
        if (!effectiveTenantId.HasValue && !User.IsPlatformOperator()) return Forbid();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _database.AuditEntries.AsNoTracking().AsQueryable();
        if (effectiveTenantId.HasValue)
            query = query.Where(item => item.TenantId == effectiveTenantId.Value);
        if (userId.HasValue) query = query.Where(item => item.UserId == userId);
        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalized = action.Trim().ToLower();
            query = query.Where(item => item.Action.ToLower().Contains(normalized));
        }
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var normalized = entityType.Trim().ToLower();
            query = query.Where(item => item.EntityType.ToLower() == normalized);
        }
        if (!string.IsNullOrWhiteSpace(entityId))
        {
            var normalized = entityId.Trim();
            query = query.Where(item => item.EntityId == normalized);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLower();
            query = query.Where(item =>
                item.Action.ToLower().Contains(normalized)
                || item.EntityType.ToLower().Contains(normalized)
                || item.EntityId.ToLower().Contains(normalized)
                || (item.Reason != null && item.Reason.ToLower().Contains(normalized)));
        }
        if (fromUtc.HasValue) query = query.Where(item => item.Timestamp >= fromUtc.Value.ToUniversalTime());
        if (toUtc.HasValue) query = query.Where(item => item.Timestamp <= toUtc.Value.ToUniversalTime());

        var total = await query.CountAsync(cancellationToken);
        var entries = await query
            .OrderByDescending(item => item.Timestamp)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        var userIds = entries.Where(item => item.UserId.HasValue)
            .Select(item => item.UserId!.Value).Distinct().ToArray();
        var users = await _database.Users.AsNoTracking()
            .Where(item => userIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Email, item.FirstName, item.LastName })
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        return Ok(new
        {
            tenantId = effectiveTenantId,
            page,
            pageSize,
            total,
            totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize),
            items = entries.Select(item => new
            {
                item.Id,
                item.TenantId,
                item.UserId,
                user = item.UserId.HasValue && users.TryGetValue(item.UserId.Value, out var actor)
                    ? new { actor.Email, actor.FirstName, actor.LastName }
                    : null,
                item.Action,
                item.EntityType,
                item.EntityId,
                item.OriginalValue,
                item.NewValue,
                item.Reason,
                item.IpAddress,
                item.UserAgent,
                item.Timestamp
            })
        });
    }
}
