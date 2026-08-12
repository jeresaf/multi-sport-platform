using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Notifications.Services;

namespace SportsPlatform.Api.Services;

public sealed class NotificationDeliveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDeliveryWorker> _logger;

    public NotificationDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationDeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Notification queue processing failed; the durable pending records will be retried.");
            }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    private async Task DrainBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        var policy = scope.ServiceProvider.GetRequiredService<ITenantNotificationDeliveryPolicy>();
        var pending = (await notifications.GetPendingNotificationsAsync(1000, cancellationToken))
            .Select(item => item.Id)
            .ToArray();
        if (pending.Length == 0) return;
        var rows = await (
            from notification in database.Notifications.AsNoTracking()
            join user in database.Users.AsNoTracking() on notification.UserId equals user.Id
            where pending.Contains(notification.Id)
            select new { notification.Id, user.TenantId })
            .ToArrayAsync(cancellationToken);
        var tenantIds = rows
            .Where(item => item.TenantId.HasValue)
            .Select(item => item.TenantId!.Value)
            .Distinct()
            .ToArray();
        var disabledTenants = await policy.GetDisabledTenantIdsAsync(
            tenantIds, DateTime.UtcNow, cancellationToken);
        var pausedCount = rows.Count(item => item.TenantId.HasValue
            && disabledTenants.Contains(item.TenantId.Value));
        var deliverable = rows
            .Where(item => !item.TenantId.HasValue
                || !disabledTenants.Contains(item.TenantId.Value))
            .Select(item => item.Id)
            .Take(100)
            .ToArray();
        if (deliverable.Length == 0) return;
        await notifications.SendBatchAsync(deliverable, cancellationToken);
        _logger.LogInformation(
            "Processed {Count} durable notifications; {PausedCount} remain paused by audited tenant support controls.",
            deliverable.Length,
            pausedCount);
    }

}
