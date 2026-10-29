using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

/// <summary>
/// How much of one quota entitlement the scope's tenant uses (S04 D15), implemented by the module that owns the items
/// it counts and registered with <see cref="QuotaUsageServiceCollectionExtensions.AddQuotaUsage{TCounter}"/>, so Tenancy
/// shows usage without referencing that module. Call after the scope's tenant is set (the items are read under RLS).
/// </summary>
public interface IQuotaUsage
{
    /// <summary>The items the tenant has now, counted as the module counts them before a create (D12).</summary>
    Task<int> CountAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Which counter counts a quota entitlement. A singleton, so the Host's startup check reads every registration without
/// creating a counter (they need a database): at most one per code, and the code must be a declared quota.
/// </summary>
public sealed record QuotaUsageRegistration(string EntitlementCode, Type CounterType);

public static class QuotaUsageServiceCollectionExtensions
{
    /// <summary>Registers <typeparamref name="TCounter"/> (scoped) as the usage counter of <paramref name="entitlementCode"/>.</summary>
    public static IServiceCollection AddQuotaUsage<TCounter>(this IServiceCollection services, string entitlementCode)
        where TCounter : class, IQuotaUsage
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(entitlementCode);
        services.TryAddScoped<TCounter>();
        return services.AddSingleton(new QuotaUsageRegistration(entitlementCode, typeof(TCounter)));
    }
}
