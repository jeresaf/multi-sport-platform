using Microsoft.Extensions.Caching.Memory;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Features.Domains;

/// <summary>
/// Host → tenant for public requests, for at most <see cref="TimeToLive"/> (docs/architecture/multi-tenancy.md). Unknown
/// hosts are cached too, so they don't reach the database on every request. Whatever changes what a host resolves to (a
/// new tenant or domain, a status, <c>publicEnabled</c>) clears the whole cache after it commits: such changes are rare.
/// Other API instances see the change when their entries expire.
/// </summary>
public sealed class TenantHostCache : IDisposable
{
    public static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(60);

    private const int _maxEntries = 10_000;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = _maxEntries });

    // Bumped by Clear: a lookup that started before a Clear doesn't store what it read.
    private long _generation;

    public async Task<TenantSummary?> GetOrLoadAsync(string host, Func<Task<TenantSummary?>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        if (_cache.TryGetValue(host, out Entry? cached))
        {
            return cached!.Tenant;
        }

        var generation = Interlocked.Read(ref _generation);
        var tenant = await load();
        if (generation == Interlocked.Read(ref _generation))
        {
            _cache.Set(host, new Entry(tenant), new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeToLive, Size = 1 });
        }

        return tenant;
    }

    public void Clear()
    {
        Interlocked.Increment(ref _generation);
        _cache.Clear();
    }

    public void Dispose() => _cache.Dispose();

    private sealed record Entry(TenantSummary? Tenant);
}
