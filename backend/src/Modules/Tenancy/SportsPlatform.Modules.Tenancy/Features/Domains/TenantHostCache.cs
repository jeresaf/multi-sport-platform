using Microsoft.Extensions.Caching.Memory;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Features.Domains;

/// <summary>
/// Host → tenant for public requests, for at most <see cref="TimeToLive"/> (docs/architecture/multi-tenancy.md). Unknown
/// hosts are cached too, so they don't reach the database on every request. Whatever changes what a host resolves to (a
/// new tenant or domain, a status, <c>publicEnabled</c>) clears the whole cache after it commits: such changes are rare.
/// Other API instances see the change when their entries expire. Expiry runs on <see cref="MemoryCache"/>'s system
/// clock, not the injected <see cref="TimeProvider"/>, so tests prove the clearing, not the expiry.
/// </summary>
public sealed class TenantHostCache : IDisposable
{
    public static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(60);

    private const int _maxEntries = 10_000;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = _maxEntries });
    private readonly Lock _gate = new();

    // Bumped by Clear: a lookup that started before a Clear doesn't store what it read. The check and the store, and
    // the bump and the clear, each happen under the gate, so a Clear can't fall between them.
    private long _generation;

    public async Task<TenantSummary?> GetOrLoadAsync(string host, Func<Task<TenantSummary?>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        if (_cache.TryGetValue(host, out Entry? cached))
        {
            return cached!.Tenant;
        }

        long generation;
        lock (_gate)
        {
            generation = _generation;
        }

        var tenant = await load();
        lock (_gate)
        {
            if (generation == _generation)
            {
                _cache.Set(host, new Entry(tenant), new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeToLive, Size = 1 });
            }
        }

        return tenant;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _generation++;
            _cache.Clear();
        }
    }

    public void Dispose() => _cache.Dispose();

    private sealed record Entry(TenantSummary? Tenant);
}
