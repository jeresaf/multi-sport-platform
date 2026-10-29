using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Features.Entitlements;

/// <summary>
/// One quota's usage (S04 D15): <c>used</c> of <c>limit</c> (null = unlimited) when a module measures it
/// (<c>measured</c>); otherwise <c>used</c> is null: not measured yet. <c>warning</c>: the tenant uses 80 % of the
/// limit or more.
/// </summary>
public sealed record QuotaUsageResponse(string Code, bool Measured, int? Used, int? Limit, bool Warning);

/// <summary>
/// The scope's tenant's usage of every quota entitlement, from the <see cref="IQuotaUsage"/> counters the owning modules
/// register (at most one per code, checked at startup). Counted on every call, never cached: usage changes with every
/// create.
/// </summary>
public sealed class QuotaUsages(IEnumerable<QuotaUsageRegistration> registrations, IServiceProvider services)
{
    private readonly Dictionary<string, Type> _counters =
        registrations.ToDictionary(registration => registration.EntitlementCode, registration => registration.CounterType, StringComparer.Ordinal);

    /// <summary>One row per quota in <paramref name="definitions"/>, in their order, with its limit from <paramref name="effective"/>.</summary>
    public async Task<IReadOnlyList<QuotaUsageResponse>> ReadAsync(
        IReadOnlyList<EntitlementDefinition> definitions, EffectiveEntitlements effective, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(effective);
        var rows = new List<QuotaUsageResponse>();
        foreach (var definition in definitions.Where(definition => definition.Type == EntitlementType.Quota))
        {
            var limit = effective.Of(definition).Value.Limit;
            if (!_counters.TryGetValue(definition.Code, out var counterType))
            {
                rows.Add(new QuotaUsageResponse(definition.Code, Measured: false, Used: null, limit, Warning: false));
                continue;
            }

            var used = await ((IQuotaUsage)services.GetRequiredService(counterType)).CountAsync(cancellationToken);
            rows.Add(new QuotaUsageResponse(definition.Code, Measured: true, used, limit, EffectiveEntitlements.IsNearLimit(limit, used)));
        }

        return rows;
    }
}
