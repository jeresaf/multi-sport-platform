using Microsoft.Extensions.Caching.Memory;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;

namespace SportsPlatform.Modules.Tenancy.Features.Access;

/// <summary>
/// What step 1 and the effective entitlements read on every admin request (S04 D7): each tenant's stored inputs (time
/// zone, subscription periods, unremoved overrides) for at most <see cref="TimeToLive"/>, and the plans' values until
/// restart (configuration data, changed by a reviewed seed). Statuses and "today" are derived on every call, never
/// cached. Whatever changes a tenant's inputs (a subscription or override command, a settings update) clears the
/// tenant's entry after it commits; other API instances see the change when their entries expire. As for
/// <see cref="Domains.TenantHostCache"/>, expiry runs on the system clock, so tests prove the clearing.
/// </summary>
public sealed class TenantAccessCache : IDisposable
{
    public static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(60);

    private const int _maxEntries = 10_000;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = _maxEntries });
    private readonly Lock _gate = new();

    // Bumped by a clear: a load that started before it doesn't store what it read (TenantHostCache).
    private long _generation;

    public Task<TenantAccessInputs> GetOrLoadAsync(Guid tenantId, Func<Task<TenantAccessInputs>> load) =>
        GetOrLoadAsync(new TenantKey(tenantId), load, TimeToLive);

    /// <summary>Every plan's values, by plan code and entitlement code.</summary>
    public Task<PlanValues> GetOrLoadPlanValuesAsync(Func<Task<PlanValues>> load) =>
        GetOrLoadAsync(PlanValuesKey.Instance, load, timeToLive: null);

    /// <summary>After a change to the tenant's settings, periods or overrides has committed.</summary>
    public void Clear(Guid tenantId)
    {
        lock (_gate)
        {
            _generation++;
            _cache.Remove(new TenantKey(tenantId));
        }
    }

    /// <summary>Everything, plan values included (tests, between database resets).</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _generation++;
            _cache.Clear();
        }
    }

    public void Dispose() => _cache.Dispose();

    private async Task<T> GetOrLoadAsync<T>(object key, Func<Task<T>> load, TimeSpan? timeToLive)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(load);
        if (_cache.TryGetValue(key, out T? cached))
        {
            return cached!;
        }

        long generation;
        lock (_gate)
        {
            generation = _generation;
        }

        var value = await load();
        lock (_gate)
        {
            if (generation == _generation)
            {
                _cache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive, Size = 1 });
            }
        }

        return value;
    }

    private sealed record TenantKey(Guid TenantId);

    private sealed record PlanValuesKey
    {
        public static readonly PlanValuesKey Instance = new();
    }
}

/// <summary>
/// A tenant's stored inputs to its access and entitlements, read without tracking and shared between requests: never
/// change them (load tracked entities to change them). <c>Overrides</c> are the unremoved ones.
/// </summary>
public sealed record TenantAccessInputs(string TimeZone, IReadOnlyList<SubscriptionPeriod> Periods, IReadOnlyList<EntitlementOverride> Overrides);

/// <summary>Every stored plan's values, active or not (a tenant may still be on a deactivated plan).</summary>
public sealed class PlanValues(IReadOnlyDictionary<string, IReadOnlyDictionary<string, EntitlementValue>> byPlan)
{
    private static readonly IReadOnlyDictionary<string, EntitlementValue> _none = new Dictionary<string, EntitlementValue>();

    /// <summary>The plan's values by entitlement code; empty for no plan or an unknown one.</summary>
    public IReadOnlyDictionary<string, EntitlementValue> Of(string? planCode) =>
        planCode is not null && byPlan.TryGetValue(planCode, out var values) ? values : _none;
}
