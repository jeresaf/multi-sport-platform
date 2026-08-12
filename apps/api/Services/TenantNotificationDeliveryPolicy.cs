using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Persistence;

namespace SportsPlatform.Api.Services;

public interface ITenantNotificationDeliveryPolicy
{
    Task<IReadOnlySet<Guid>> GetDisabledTenantIdsAsync(
        IEnumerable<Guid> tenantIds,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}

public sealed class TenantNotificationDeliveryPolicy : ITenantNotificationDeliveryPolicy
{
    private readonly SportsPlatformDbContext _database;

    public TenantNotificationDeliveryPolicy(SportsPlatformDbContext database)
    {
        _database = database;
    }

    public async Task<IReadOnlySet<Guid>> GetDisabledTenantIdsAsync(
        IEnumerable<Guid> tenantIds,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var ids = tenantIds.Distinct().ToArray();
        if (ids.Length == 0) return new HashSet<Guid>();
        var controls = await _database.AuditEntries.AsNoTracking()
            .Where(item => item.TenantId.HasValue
                && ids.Contains(item.TenantId.Value)
                && (item.Action == "PlatformSupportDisabledNotifications"
                    || item.Action == "PlatformSupportEnabledNotifications"))
            .OrderByDescending(item => item.Timestamp)
            .ToArrayAsync(cancellationToken);
        return controls
            .GroupBy(item => item.TenantId!.Value)
            .Select(group => group.First())
            .Where(item => IsActivePause(item.Action, item.NewValue, nowUtc))
            .Select(item => item.TenantId!.Value)
            .ToHashSet();
    }

    private static bool IsActivePause(string action, string? values, DateTime nowUtc)
    {
        if (action != "PlatformSupportDisabledNotifications"
            || string.IsNullOrWhiteSpace(values)) return false;
        using var document = JsonDocument.Parse(values);
        return document.RootElement.TryGetProperty("disabledUntilUtc", out var until)
            && until.ValueKind == JsonValueKind.String
            && until.TryGetDateTime(out var parsed)
            && parsed.ToUniversalTime() > nowUtc;
    }
}
