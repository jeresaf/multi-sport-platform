using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Subscriptions.Entities;
using SportsPlatform.Infrastructure.Persistence;

namespace SportsPlatform.Infrastructure.Services;

public interface ISubscriptionsService
{
    Task<Subscription> CreateSubscriptionAsync(Guid tenantId, string plan, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<Subscription?> GetActiveSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Subscription>> GetSubscriptionHistoryAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task ActivateSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task SuspendSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task UpdateFeatureEntitlementsAsync(Guid subscriptionId, string featureEntitlementsJson, CancellationToken cancellationToken = default);
    Task UpdateUsageLimitsAsync(Guid subscriptionId, string usageLimitsJson, CancellationToken cancellationToken = default);
    Task<Subscription?> GetByIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task MarkExpiringAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task SetGracePeriodAsync(Guid subscriptionId, DateTime graceEnd, CancellationToken cancellationToken = default);
    Task SetReadOnlyAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task UnsuspendAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task SetAutoRenewAsync(Guid subscriptionId, bool autoRenew, CancellationToken cancellationToken = default);
    Task RenewAsync(Guid subscriptionId, DateTime newEndDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Subscription>> GetSubscriptionsDueForTransitionAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

public class SubscriptionsService : ISubscriptionsService
{
    private readonly SportsPlatformDbContext _context;
    private readonly DbSet<Subscription> _subscriptions;

    public SubscriptionsService(SportsPlatformDbContext context)
    {
        _context = context;
        _subscriptions = context.Set<Subscription>();
    }

    public async Task<Subscription> CreateSubscriptionAsync(Guid tenantId, string plan, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var subscription = new Subscription(Guid.NewGuid(), tenantId, plan, startDate, endDate);
        _subscriptions.Add(subscription);
        await _context.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    public async Task<Subscription?> GetActiveSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _subscriptions
            .Where(s => s.TenantId == tenantId
                && s.Status != SubscriptionStatus.Draft)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Subscription>> GetSubscriptionHistoryAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _subscriptions
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Subscription?> GetByIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _subscriptions.FindAsync(new object[] { subscriptionId }, cancellationToken);
    }

    public async Task ActivateSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptions.FindAsync(new object[] { subscriptionId }, cancellationToken);
        if (subscription != null)
        {
            subscription.Activate();
            _subscriptions.Update(subscription);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SuspendSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptions.FindAsync(new object[] { subscriptionId }, cancellationToken);
        if (subscription != null)
        {
            subscription.Suspend();
            _subscriptions.Update(subscription);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkExpiringAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptions.FindAsync(new object[] { subscriptionId }, cancellationToken);
        if (subscription != null)
        {
            subscription.MarkExpiring();
            _subscriptions.Update(subscription);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SetGracePeriodAsync(Guid subscriptionId, DateTime graceEnd, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptions.FindAsync(new object[] { subscriptionId }, cancellationToken);
        if (subscription != null)
        {
            subscription.SetGracePeriod(graceEnd);
            _subscriptions.Update(subscription);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SetReadOnlyAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptions.FindAsync(new object[] { subscriptionId }, cancellationToken);
        if (subscription != null)
        {
            subscription.SetReadOnly();
            _subscriptions.Update(subscription);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UnsuspendAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptions.FindAsync(new object[] { subscriptionId }, cancellationToken);
        if (subscription != null)
        {
            subscription.Unsuspend();
            _subscriptions.Update(subscription);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ArchiveAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptions.FindAsync(new object[] { subscriptionId }, cancellationToken);
        if (subscription != null)
        {
            subscription.Archive();
            _subscriptions.Update(subscription);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SetAutoRenewAsync(Guid subscriptionId, bool autoRenew, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptions.FindAsync(new object[] { subscriptionId }, cancellationToken);
        if (subscription != null)
        {
            subscription.SetAutoRenew(autoRenew);
            _subscriptions.Update(subscription);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RenewAsync(
        Guid subscriptionId,
        DateTime newEndDate,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptions.FindAsync(
            new object[] { subscriptionId },
            cancellationToken);
        if (subscription is null) return;
        subscription.Renew(newEndDate);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateFeatureEntitlementsAsync(Guid subscriptionId, string featureEntitlementsJson, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptions.FindAsync(new object[] { subscriptionId }, cancellationToken);
        if (subscription != null)
        {
            var currentLimits = subscription.UsageLimits;
            subscription.ConfigureFeatures(featureEntitlementsJson, currentLimits);
            _subscriptions.Update(subscription);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateUsageLimitsAsync(Guid subscriptionId, string usageLimitsJson, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptions.FindAsync(new object[] { subscriptionId }, cancellationToken);
        if (subscription != null)
        {
            var currentEntitlements = subscription.FeatureEntitlements;
            subscription.ConfigureFeatures(currentEntitlements, usageLimitsJson);
            _subscriptions.Update(subscription);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Subscription>> GetSubscriptionsDueForTransitionAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _subscriptions
            .Where(s => s.TenantId == tenantId)
            .Where(s => s.Status != SubscriptionStatus.Archived)
            .Where(s =>
                // Active subscriptions past end date
                (s.Status == SubscriptionStatus.Active && s.EndDate < now) ||
                // Expiring subscriptions past end date
                (s.Status == SubscriptionStatus.Expiring && s.EndDate < now) ||
                // Grace period subscriptions past grace period end
                (s.Status == SubscriptionStatus.GracePeriod && s.GracePeriodEnd != null && s.GracePeriodEnd < now) ||
                (s.Status == SubscriptionStatus.ReadOnly
                    && s.UpdatedAt != null
                    && s.UpdatedAt <= now.AddDays(-30)) ||
                // Active subscriptions approaching end date (within 7 days)
                (s.Status == SubscriptionStatus.Active && s.EndDate >= now && s.EndDate <= now.AddDays(7)))
            .OrderBy(s => s.EndDate)
            .ToListAsync(cancellationToken);
    }
}
