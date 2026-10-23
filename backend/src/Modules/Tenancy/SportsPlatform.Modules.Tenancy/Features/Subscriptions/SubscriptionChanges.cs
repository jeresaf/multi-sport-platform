using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Domain.Plans;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions;

/// <summary>
/// What the subscription commands share (S04 D3, D4). The Host runs them under the route's tenant, so the periods and
/// settings are read under RLS and the audit entry carries the tenant. A change takes the tenant's subscription lock
/// first, then reads every period: two changes to one tenant queue on the lock, so the second sees the first's period
/// and both can't pass the overlap check. One save, then the commit.
/// </summary>
public sealed class SubscriptionChanges(TenancyDbContext context, AuditScope audit, TimeProvider timeProvider)
{
    public const string AuditEntityType = "SubscriptionPeriod";

    /// <summary>The subscription as it stands, without the lock (reads).</summary>
    public async Task<Result<SubscriptionResponse>> ReadAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!await context.Set<Tenant>().AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            return TenancyErrors.NotFound;
        }

        var (subscription, today, timeZone) = await LoadAsync(tenantId, cancellationToken);
        return Result.Success(SubscriptionResponse.From(context, tenantId, subscription, today, timeZone));
    }

    /// <summary>
    /// Runs <paramref name="change"/> on the tenant's periods under its subscription lock and saves what it changed.
    /// <paramref name="change"/> gets the tenant's date and returns the period the audit entry names, or the failure.
    /// </summary>
    public async Task<Result<SubscriptionResponse>> ChangeAsync(
        Guid tenantId,
        Func<TenantSubscription, DateOnly, Result<SubscriptionChange>> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!await context.Set<Tenant>().AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            return TenancyErrors.NotFound;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(tenantId, cancellationToken);

        // Read after the lock: a new statement, so in READ COMMITTED it sees every change committed before it.
        var (subscription, today, timeZone) = await LoadAsync(tenantId, cancellationToken);
        var changed = change(subscription, today);
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        foreach (var period in subscription.Periods.Where(p => context.Entry(p).State == EntityState.Detached))
        {
            context.Add(period);
        }

        audit.Describe(AuditEntityType, changed.Value.AuditedPeriod.Id, metadata: changed.Value.Metadata);
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return Result.Success(SubscriptionResponse.From(context, tenantId, subscription, today, timeZone));
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

    private async Task<(TenantSubscription Subscription, DateOnly Today, string TimeZone)> LoadAsync(
        Guid tenantId, CancellationToken cancellationToken)
    {
        var timeZone = await context.Set<TenantSettings>()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.TimeZone)
            .SingleAsync(cancellationToken);
        var periods = await context.Set<SubscriptionPeriod>().ToListAsync(cancellationToken);
        return (new TenantSubscription(periods), TenantCalendar.Today(timeProvider, timeZone), timeZone);
    }

    /// <summary>
    /// The tenant's subscription lock, held until the transaction ends. Key: <c>&lt;schema&gt;.&lt;purpose&gt;:&lt;id&gt;</c>
    /// (data-conventions.md), hashed to PostgreSQL's 64-bit advisory lock space.
    /// </summary>
    private async Task LockAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var key = $"tenancy.subscription:{tenantId}";
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}

/// <summary>What a command changed: the period its audit entry names, and the entry's metadata.</summary>
public sealed record SubscriptionChange(SubscriptionPeriod AuditedPeriod, SubscriptionAuditMetadata Metadata);

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
