using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Infrastructure.Services;

public sealed class PrivacyRetentionService
{
    private readonly SportsPlatformDbContext _database;
    private readonly IAuditService _audit;

    public PrivacyRetentionService(SportsPlatformDbContext database, IAuditService audit)
    {
        _database = database;
        _audit = audit;
    }

    public async Task<int> ExecuteAsync(
        TenantRetentionPolicy policy,
        Guid? actorId = null,
        string reason = "Scheduled tenant retention run",
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-policy.RetentionDays);
        var tenantUserIds = _database.Users
            .Where(item => item.TenantId == policy.TenantId)
            .Select(item => item.Id);
        var affected = 0;

        switch (policy.Resource)
        {
            case RetentionResource.Notifications:
            {
                var records = await _database.Notifications
                    .Where(item => tenantUserIds.Contains(item.UserId) && item.CreatedAt < cutoff)
                    .ToArrayAsync(cancellationToken);
                _database.Notifications.RemoveRange(records);
                affected = records.Length;
                break;
            }
            case RetentionResource.DeviceSessions:
            {
                var records = await _database.DeviceSessions
                    .Where(item => tenantUserIds.Contains(item.UserId)
                        && item.CreatedAt < cutoff
                        && (item.IsRevoked || item.RefreshTokenExpiry < DateTime.UtcNow))
                    .ToArrayAsync(cancellationToken);
                _database.DeviceSessions.RemoveRange(records);
                affected = records.Length;
                break;
            }
            case RetentionResource.PrivacyRequests:
            {
                var records = await _database.PrivacyActionRequests
                    .Where(item => item.TenantId == policy.TenantId
                        && item.CompletedAt != null
                        && item.CompletedAt < cutoff)
                    .ToArrayAsync(cancellationToken);
                if (policy.Action == RetentionAction.Delete)
                    _database.PrivacyActionRequests.RemoveRange(records);
                else
                    foreach (var record in records) record.Redact();
                affected = records.Length;
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(policy.Resource));
        }

        policy.MarkExecuted(affected);
        await _database.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync(
            "PrivacyRetentionExecuted",
            nameof(TenantRetentionPolicy),
            policy.Id.ToString(),
            policy.TenantId,
            actorId,
            newValue: JsonSerializer.Serialize(new
            {
                policy.Resource,
                policy.Action,
                policy.RetentionDays,
                affected,
                cutoff
            }),
            reason: reason,
            cancellationToken: cancellationToken);
        return affected;
    }
}
