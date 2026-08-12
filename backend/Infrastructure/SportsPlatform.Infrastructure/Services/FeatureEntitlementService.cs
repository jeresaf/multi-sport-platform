using System.Text.Json;
using SportsPlatform.Modules.Subscriptions.Entities;

namespace SportsPlatform.Infrastructure.Services;

/// <summary>
/// Plan-based feature gating service that checks whether a tenant's subscription
/// entitles them to use specific features.
/// </summary>
public interface IFeatureEntitlementService
{
    Task<bool> HasFeatureAsync(Guid tenantId, string featureCode, CancellationToken cancellationToken = default);
    Task<Dictionary<string, bool>> GetFeatureFlagsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task SetFeatureEntitlementsAsync(Guid subscriptionId, Dictionary<string, bool> entitlements, CancellationToken cancellationToken = default);
    Task<Dictionary<string, bool>> GetPlanDefaultsAsync(string plan);
}

public class FeatureEntitlementService : IFeatureEntitlementService
{
    private readonly ISubscriptionsService _subscriptionsService;

    // Plan-based default feature entitlements
    private static readonly Dictionary<string, Dictionary<string, bool>> PlanDefaults = new()
    {
        ["free"] = new()
        {
            ["sports.max_sports"] = true,
            ["sports.max_competitions"] = true,
            ["sports.max_participants"] = true,
            ["branding.custom_logo"] = false,
            ["branding.custom_colors"] = false,
            ["branding.custom_domain"] = false,
            ["analytics.basic"] = true,
            ["analytics.advanced"] = false,
            ["api.rate_limit"] = true,
            ["api.webhooks"] = false,
            ["export.csv"] = true,
            ["export.pdf"] = false,
            ["sponsorship.placements"] = false,
            ["users.max_admins"] = true,
            ["users.max_operators"] = true,
            [UsageLimitsService.HardLimitsFeature] = true,
            ["support.email"] = true,
            ["support.priority"] = false,
        },
        ["starter"] = new()
        {
            ["sports.max_sports"] = true,
            ["sports.max_competitions"] = true,
            ["sports.max_participants"] = true,
            ["branding.custom_logo"] = true,
            ["branding.custom_colors"] = true,
            ["branding.custom_domain"] = false,
            ["analytics.basic"] = true,
            ["analytics.advanced"] = true,
            ["api.rate_limit"] = true,
            ["api.webhooks"] = true,
            ["export.csv"] = true,
            ["export.pdf"] = true,
            ["sponsorship.placements"] = true,
            ["users.max_admins"] = true,
            ["users.max_operators"] = true,
            [UsageLimitsService.HardLimitsFeature] = true,
            ["support.email"] = true,
            ["support.priority"] = false,
        },
        ["professional"] = new()
        {
            ["sports.max_sports"] = true,
            ["sports.max_competitions"] = true,
            ["sports.max_participants"] = true,
            ["branding.custom_logo"] = true,
            ["branding.custom_colors"] = true,
            ["branding.custom_domain"] = true,
            ["analytics.basic"] = true,
            ["analytics.advanced"] = true,
            ["api.rate_limit"] = true,
            ["api.webhooks"] = true,
            ["export.csv"] = true,
            ["export.pdf"] = true,
            ["sponsorship.placements"] = true,
            ["users.max_admins"] = true,
            ["users.max_operators"] = true,
            [UsageLimitsService.HardLimitsFeature] = true,
            ["support.email"] = true,
            ["support.priority"] = true,
        },
        ["enterprise"] = new()
        {
            ["sports.max_sports"] = true,
            ["sports.max_competitions"] = true,
            ["sports.max_participants"] = true,
            ["branding.custom_logo"] = true,
            ["branding.custom_colors"] = true,
            ["branding.custom_domain"] = true,
            ["analytics.basic"] = true,
            ["analytics.advanced"] = true,
            ["api.rate_limit"] = true,
            ["api.webhooks"] = true,
            ["export.csv"] = true,
            ["export.pdf"] = true,
            ["sponsorship.placements"] = true,
            ["users.max_admins"] = true,
            ["users.max_operators"] = true,
            [UsageLimitsService.HardLimitsFeature] = true,
            ["support.email"] = true,
            ["support.priority"] = true,
        },
    };

    public FeatureEntitlementService(ISubscriptionsService subscriptionsService)
    {
        _subscriptionsService = subscriptionsService;
    }

    public async Task<bool> HasFeatureAsync(Guid tenantId, string featureCode, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionsService.GetActiveSubscriptionAsync(tenantId, cancellationToken);
        if (subscription == null || !subscription.CanWrite())
            return false;

        var flags = await GetFeatureFlagsAsync(tenantId, cancellationToken);
        return flags.TryGetValue(featureCode, out var enabled) && enabled;
    }

    public async Task<Dictionary<string, bool>> GetFeatureFlagsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionsService.GetActiveSubscriptionAsync(tenantId, cancellationToken);
        if (subscription == null)
            return new Dictionary<string, bool>();

        // Start with plan defaults
        var defaults = GetPlanDefaultsSync(subscription.Plan);
        var result = new Dictionary<string, bool>(defaults);

        // Override with any custom entitlements stored on the subscription
        if (!string.IsNullOrEmpty(subscription.FeatureEntitlements) && subscription.FeatureEntitlements != "{}")
        {
            try
            {
                var customEntitlements = JsonSerializer.Deserialize<Dictionary<string, bool>>(subscription.FeatureEntitlements);
                if (customEntitlements != null)
                {
                    foreach (var kvp in customEntitlements)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (JsonException)
            {
                // If custom entitlements are malformed, fall back to plan defaults
            }
        }

        return result;
    }

    public async Task SetFeatureEntitlementsAsync(Guid subscriptionId, Dictionary<string, bool> entitlements, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(entitlements);
        await _subscriptionsService.UpdateFeatureEntitlementsAsync(subscriptionId, json, cancellationToken);
    }

    public Task<Dictionary<string, bool>> GetPlanDefaultsAsync(string plan)
    {
        var normalizedPlan = plan.ToLowerInvariant();
        if (PlanDefaults.TryGetValue(normalizedPlan, out var defaults))
        {
            return Task.FromResult(new Dictionary<string, bool>(defaults));
        }
        return Task.FromResult(new Dictionary<string, bool>(PlanDefaults["free"]));
    }

    private Dictionary<string, bool> GetPlanDefaultsSync(string plan)
    {
        var normalizedPlan = plan.ToLowerInvariant();
        if (PlanDefaults.TryGetValue(normalizedPlan, out var defaults))
        {
            return new Dictionary<string, bool>(defaults);
        }
        return new Dictionary<string, bool>(PlanDefaults["free"]);
    }
}
