using Microsoft.Extensions.Logging;
using SportsPlatform.Infrastructure.Services;

namespace SportsPlatform.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire background job that manages the subscription lifecycle state machine.
/// Runs periodically to transition subscriptions through their lifecycle:
/// Active → Expiring → GracePeriod → ReadOnly → Archived
/// </summary>
public class SubscriptionLifecycleJob
{
    private readonly ISubscriptionsService _subscriptionsService;
    private readonly ILogger<SubscriptionLifecycleJob> _logger;

    public SubscriptionLifecycleJob(
        ISubscriptionsService subscriptionsService,
        ILogger<SubscriptionLifecycleJob> logger)
    {
        _subscriptionsService = subscriptionsService;
        _logger = logger;
    }

    /// <summary>
    /// Main job execution - processes all subscriptions due for state transitions.
    /// Should be scheduled via Hangfire RecurringJob (e.g., every hour).
    /// </summary>
    public async Task ExecuteAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException(
                "An explicit tenant ID is required.",
                nameof(tenantId));

        _logger.LogInformation(
            "Subscription lifecycle job started for tenant {TenantId} at {Time}",
            tenantId,
            DateTime.UtcNow);

        try
        {
            var dueSubscriptions =
                await _subscriptionsService.GetSubscriptionsDueForTransitionAsync(
                    tenantId,
                    cancellationToken);
            _logger.LogInformation(
                "Found {Count} subscriptions due for transition for tenant {TenantId}",
                dueSubscriptions.Count,
                tenantId);

            foreach (var subscription in dueSubscriptions)
            {
                await ProcessSubscriptionAsync(subscription, cancellationToken);
            }

            _logger.LogInformation(
                "Subscription lifecycle job completed for tenant {TenantId} at {Time}",
                tenantId,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error executing subscription lifecycle job for tenant {TenantId}",
                tenantId);
            throw;
        }
    }

    private async Task ProcessSubscriptionAsync(
        SportsPlatform.Modules.Subscriptions.Entities.Subscription subscription,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var subscriptionId = subscription.Id;

        try
        {
            switch (subscription.Status)
            {
                case SportsPlatform.Modules.Subscriptions.Entities.SubscriptionStatus.Active:
                    await ProcessActiveSubscription(subscription, now, cancellationToken);
                    break;

                case SportsPlatform.Modules.Subscriptions.Entities.SubscriptionStatus.Expiring:
                    await ProcessExpiringSubscription(subscription, now, cancellationToken);
                    break;

                case SportsPlatform.Modules.Subscriptions.Entities.SubscriptionStatus.GracePeriod:
                    await ProcessGracePeriodSubscription(subscription, now, cancellationToken);
                    break;

                case SportsPlatform.Modules.Subscriptions.Entities.SubscriptionStatus.ReadOnly:
                    await ProcessReadOnlySubscription(subscription, now, cancellationToken);
                    break;

                default:
                    _logger.LogDebug("Subscription {Id} in status {Status} - no action needed",
                        subscriptionId, subscription.Status);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing subscription {Id} in status {Status}",
                subscriptionId, subscription.Status);
        }
    }

    private async Task ProcessActiveSubscription(
        SportsPlatform.Modules.Subscriptions.Entities.Subscription subscription,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var daysUntilExpiry = (subscription.EndDate - now).TotalDays;

        if (now >= subscription.EndDate)
        {
            // Subscription has expired - move to Expiring or GracePeriod
            if (subscription.AutoRenew)
            {
                // Auto-renew: create new subscription period
                _logger.LogInformation("Auto-renewing subscription {Id}", subscription.Id);
                var newStartDate = subscription.EndDate;
                var newEndDate = subscription.EndDate.AddYears(1);
                await _subscriptionsService.CreateSubscriptionAsync(
                    subscription.TenantId,
                    subscription.Plan,
                    newStartDate,
                    newEndDate,
                    cancellationToken);

                // Mark old subscription as archived
                await _subscriptionsService.ArchiveAsync(subscription.Id, cancellationToken);
            }
            else
            {
                // No auto-renew: move to grace period
                _logger.LogInformation("Subscription {Id} expired, moving to grace period", subscription.Id);
                var graceEnd = subscription.EndDate.AddDays(7);
                await _subscriptionsService.SetGracePeriodAsync(subscription.Id, graceEnd, cancellationToken);
            }
        }
        else if (daysUntilExpiry <= 7)
        {
            // Within 7 days of expiry - mark as expiring
            _logger.LogInformation("Subscription {Id} is expiring in {Days:F1} days", subscription.Id, daysUntilExpiry);
            await _subscriptionsService.MarkExpiringAsync(subscription.Id, cancellationToken);
        }
    }

    private async Task ProcessExpiringSubscription(
        SportsPlatform.Modules.Subscriptions.Entities.Subscription subscription,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (now >= subscription.EndDate)
        {
            // Expiry date has passed
            if (subscription.AutoRenew)
            {
                _logger.LogInformation("Auto-renewing expiring subscription {Id}", subscription.Id);
                var newStartDate = subscription.EndDate;
                var newEndDate = subscription.EndDate.AddYears(1);
                await _subscriptionsService.CreateSubscriptionAsync(
                    subscription.TenantId,
                    subscription.Plan,
                    newStartDate,
                    newEndDate,
                    cancellationToken);
                await _subscriptionsService.ArchiveAsync(subscription.Id, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Expiring subscription {Id} moving to grace period", subscription.Id);
                var graceEnd = subscription.EndDate.AddDays(7);
                await _subscriptionsService.SetGracePeriodAsync(subscription.Id, graceEnd, cancellationToken);
            }
        }
    }

    private async Task ProcessGracePeriodSubscription(
        SportsPlatform.Modules.Subscriptions.Entities.Subscription subscription,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (subscription.GracePeriodEnd.HasValue && now >= subscription.GracePeriodEnd.Value)
        {
            // Grace period has ended - move to read-only
            _logger.LogInformation("Grace period ended for subscription {Id}, moving to read-only", subscription.Id);
            await _subscriptionsService.SetReadOnlyAsync(subscription.Id, cancellationToken);
        }
    }

    private async Task ProcessReadOnlySubscription(
        SportsPlatform.Modules.Subscriptions.Entities.Subscription subscription,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // After 30 days in read-only mode, archive the subscription
        if (subscription.UpdatedAt.HasValue && (now - subscription.UpdatedAt.Value).TotalDays >= 30)
        {
            _logger.LogInformation("Read-only subscription {Id} being archived after 30 days", subscription.Id);
            await _subscriptionsService.ArchiveAsync(subscription.Id, cancellationToken);
        }
    }
}
