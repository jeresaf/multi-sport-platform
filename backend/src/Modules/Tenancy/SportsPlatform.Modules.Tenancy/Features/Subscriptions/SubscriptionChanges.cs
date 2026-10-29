using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Plans;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions;

/// <summary>
/// What the subscription and override commands share (S04 D3, D4, D5). The Host runs them under the route's tenant, so
/// the periods, overrides and settings are read under RLS and the audit entry carries the tenant. A change takes the
/// tenant's lock for what it changes first (periods or overrides), then reads: two changes of one kind to one tenant
/// queue on the lock, so the second sees the first's rows and both can't pass the overlap check or add a second
/// override of one entitlement. One save, then the commit.
/// </summary>
public sealed class SubscriptionChanges(TenancyDbContext context, AuditScope audit, TimeProvider timeProvider)
{
    public const string AuditEntityType = "SubscriptionPeriod";
    public const string OverrideAuditEntityType = "EntitlementOverride";

    /// <summary>The subscription as it stands, without a lock (reads).</summary>
    public async Task<Result<SubscriptionResponse>> ReadAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!await context.Set<Tenant>().AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            return TenancyErrors.NotFound;
        }

        return Result.Success(Respond(tenantId, await LoadAsync(tenantId, cancellationToken)));
    }

    /// <summary>
    /// Runs <paramref name="change"/> on the tenant's periods under its subscription lock and saves what it changed.
    /// <paramref name="change"/> gets the tenant's date and returns the period the audit entry names, or the failure.
    /// </summary>
    public Task<Result<SubscriptionResponse>> ChangeAsync(
        Guid tenantId,
        Func<TenantSubscription, DateOnly, Result<SubscriptionChange>> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        return RunAsync(
            tenantId,
            "subscription",
            (loaded, today) =>
            {
                var changed = change(loaded.Subscription, today);
                return changed.IsFailure
                    ? changed.Error
                    : Result.Success(new AuditedChange(AuditEntityType, changed.Value.AuditedPeriod.Id, changed.Value.Metadata));
            },
            cancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="change"/> on the tenant's overrides under its override lock and saves what it changed.
    /// <paramref name="change"/> gets the tenant's date and returns the override the audit entry names, or the failure.
    /// </summary>
    public Task<Result<SubscriptionResponse>> ChangeOverridesAsync(
        Guid tenantId,
        Func<TenantEntitlementOverrides, DateOnly, Result<OverrideChange>> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        return RunAsync(
            tenantId,
            "entitlement_override",
            (loaded, today) =>
            {
                var changed = change(loaded.Overrides, today);
                return changed.IsFailure
                    ? changed.Error
                    : Result.Success(new AuditedChange(OverrideAuditEntityType, changed.Value.AuditedOverride.Id, changed.Value.Metadata));
            },
            cancellationToken);
    }

    /// <summary>Every plan's rank, active or not, and the codes of the active ones.</summary>
    public async Task<(IReadOnlyDictionary<string, int> Ranks, IReadOnlySet<string> Active)> PlansAsync(CancellationToken cancellationToken)
    {
        var plans = await context.Set<Plan>().AsNoTracking()
            .Select(p => new { p.Code, p.Rank, p.IsActive })
            .ToListAsync(cancellationToken);
        return (
            plans.ToDictionary(p => p.Code, p => p.Rank, StringComparer.Ordinal),
            plans.Where(p => p.IsActive).Select(p => p.Code).ToHashSet(StringComparer.Ordinal));
    }

    private async Task<Result<SubscriptionResponse>> RunAsync(
        Guid tenantId, string lockPurpose, Func<Loaded, DateOnly, Result<AuditedChange>> change, CancellationToken cancellationToken)
    {
        if (!await context.Set<Tenant>().AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            return TenancyErrors.NotFound;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(lockPurpose, tenantId, cancellationToken);

        // Read after the lock: a new statement, so in READ COMMITTED it sees every change committed before it.
        var loaded = await LoadAsync(tenantId, cancellationToken);
        var changed = change(loaded, loaded.Today);
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        foreach (var added in loaded.Subscription.Periods.Cast<object>().Concat(loaded.Overrides.Overrides)
            .Where(entity => context.Entry(entity).State == EntityState.Detached))
        {
            context.Add(added);
        }

        audit.Describe(changed.Value.EntityType, changed.Value.EntityId, metadata: changed.Value.Metadata);
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return Result.Success(Respond(tenantId, loaded));
    }

    private SubscriptionResponse Respond(Guid tenantId, Loaded loaded) =>
        SubscriptionResponse.From(context, tenantId, loaded.Subscription, loaded.Overrides, loaded.Today, loaded.TimeZone);

    private async Task<Loaded> LoadAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var timeZone = await context.Set<TenantSettings>()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.TimeZone)
            .SingleAsync(cancellationToken);
        var periods = await context.Set<SubscriptionPeriod>().ToListAsync(cancellationToken);
        var overrides = await context.Set<EntitlementOverride>().ToListAsync(cancellationToken);
        return new Loaded(
            new TenantSubscription(periods), new TenantEntitlementOverrides(overrides), TenantCalendar.Today(timeProvider, timeZone), timeZone);
    }

    /// <summary>
    /// One of the tenant's locks (<c>subscription</c> or <c>entitlement_override</c>), held until the transaction ends.
    /// Key: <c>&lt;schema&gt;.&lt;purpose&gt;:&lt;id&gt;</c> (data-conventions.md), hashed to PostgreSQL's 64-bit advisory
    /// lock space.
    /// </summary>
    private async Task LockAsync(string purpose, Guid tenantId, CancellationToken cancellationToken)
    {
        var key = $"tenancy.{purpose}:{tenantId}";
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}

/// <summary>What a command changed: the period its audit entry names, and the entry's metadata.</summary>
public sealed record SubscriptionChange(SubscriptionPeriod AuditedPeriod, SubscriptionAuditMetadata Metadata);

/// <summary>What an override command changed: the override its audit entry names, and the entry's metadata.</summary>
public sealed record OverrideChange(EntitlementOverride AuditedOverride, OverrideAuditMetadata Metadata);

/// <summary>
/// The audit metadata of an override change: the entitlement, the new value and last day (none for a removal) and the
/// override a grant replaced. No personal data; the reason is the entry's own.
/// </summary>
public sealed record OverrideAuditMetadata(
    string EntitlementCode, EntitlementValue? Value, DateOnly? EffectiveUntil, Guid? ReplacedOverrideId);

internal sealed record AuditedChange(string EntityType, Guid EntityId, object Metadata);

internal sealed record Loaded(TenantSubscription Subscription, TenantEntitlementOverrides Overrides, DateOnly Today, string TimeZone);

/// <summary>
/// The audit metadata of a subscription change: the new period's plan and dates (none for a cancel) and the periods it
/// cancelled. No personal data.
/// </summary>
public sealed record SubscriptionAuditMetadata(string? PlanCode, DateOnly? StartsOn, DateOnly? EndsOn, IReadOnlyList<Guid> CancelledPeriodIds)
{
    public static SubscriptionAuditMetadata Added(SubscriptionPeriod period, params IReadOnlyList<Guid> cancelled)
    {
        ArgumentNullException.ThrowIfNull(period);
        return new(period.PlanCode, period.StartsOn, period.EndsOn, cancelled);
    }
}
