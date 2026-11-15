using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Contracts.Sports;
using SportsPlatform.Modules.Tenancy.Domain.Sports;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Sports;

/// <summary>
/// The scope's tenant's enabled sport variants (open <c>tenancy.tenant_sports</c> rows, read under RLS): what
/// <see cref="IEnabledSports"/> answers, and the <c>MAX_ENABLED_SPORTS</c> usage counter (S05 D13), which counts the
/// distinct sports of those variants as <see cref="ISportDefinitions"/> names them.
/// </summary>
public sealed class EnabledSports(TenancyDbContext context, ISportDefinitions definitions) : IEnabledSports, IQuotaUsage
{
    public const string Code = "MAX_ENABLED_SPORTS";

    public async Task<IReadOnlyList<EnabledSport>> ListAsync(CancellationToken cancellationToken) =>
        await context.Set<TenantSport>().AsNoTracking()
            .Where(s => s.DisabledAt == null)
            .OrderBy(s => s.Variant)
            .Select(s => new EnabledSport(s.Variant, s.EnabledAt))
            .ToListAsync(cancellationToken);

    public Task<bool> IsEnabledAsync(string variant, CancellationToken cancellationToken) =>
        context.Set<TenantSport>().AnyAsync(s => s.Variant == variant && s.DisabledAt == null, cancellationToken);

    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        var enabled = await context.Set<TenantSport>().AsNoTracking().Where(s => s.DisabledAt == null).ToListAsync(cancellationToken);
        return new TenantSports(enabled).SportsCounted(SportOf(await definitions.ListAsync(cancellationToken)));
    }

    /// <summary>A variant's sport among <paramref name="variants"/>; null for one that isn't published.</summary>
    public static Func<string, string?> SportOf(IReadOnlyList<SportVariant> variants)
    {
        ArgumentNullException.ThrowIfNull(variants);
        var sports = variants.ToDictionary(v => v.Variant, v => v.Sport, StringComparer.Ordinal);
        return variant => sports.GetValueOrDefault(variant);
    }
}
