using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Plans;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Features.Entitlements.GrantOverride;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions.AssignPeriod;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Seed.Demo;

/// <summary>
/// The demo tenants' subscriptions (S04 D18, docs/product/pilot-customer.md): KMSA on STANDARD, complimentary, for six
/// months (the pilot offer); ELSSC on STANDARD, paid, for 45 days (the event pass). Each starts on the seed date in the
/// tenant's time zone, through <c>subscription.assign</c> as the system actor. ELSSC's event pass also allows one active
/// edition until its period ends: the override <c>MAX_ACTIVE_EDITIONS = 1</c>, through <c>entitlement.override</c>. A
/// tenant that has any period (or, for the override, any override) is left alone, so a period that has since ended needs
/// <c>seed --reset</c>.
/// </summary>
public sealed class DemoSubscriptionSeeder : ISeeder
{
    /// <summary>Bump when the seeder writes something different from the same data.</summary>
    private const int _version = 2;

    internal static readonly IReadOnlyList<DemoSubscription> Subscriptions =
    [
        new("kmsa", SubscriptionKind.Complimentary, Months: 6, Days: 0),
        new("elssc", SubscriptionKind.Paid, Months: 0, Days: 45),
    ];

    /// <summary>Each until the end of the tenant's period.</summary>
    internal static readonly IReadOnlyList<DemoOverride> Overrides =
    [
        new("elssc", TenancyEntitlements.MaxActiveEditions.Code, EntitlementValue.Quota(1), "Event pass: one active edition, the Inter-Schools Football Festival"),
    ];

    public string Key => "tenancy.demo.subscriptions";

    public SeedCategory Category => SeedCategory.Demo;

    /// <summary>
    /// After the tenants (0), which it needs; before the invitations (10), so each tenant has its plan when invitations
    /// are counted against the staff quota (S04-T3, D13).
    /// </summary>
    public int Order => 5;

    public string Checksum { get; } = ComputeChecksum();

    public async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var inserted = 0;
        foreach (var demo in Subscriptions)
        {
            var tenantId = DemoTenantSeeder.TenantId(demo.Slug);
            var hasPeriod = await context.QueryAsTenantAsync(
                tenantId, (services, ct) => services.GetRequiredService<TenancyDbContext>().Set<SubscriptionPeriod>().AnyAsync(ct), cancellationToken);
            if (hasPeriod)
            {
                continue;
            }

            await context.RunCommandAsync(
                TenancyActions.SubscriptionAssign, tenantId, (services, ct) => AssignAsync(services, tenantId, demo, ct), cancellationToken);
            inserted++;
        }

        foreach (var demo in Overrides)
        {
            var tenantId = DemoTenantSeeder.TenantId(demo.Slug);
            var endsOn = await context.QueryAsTenantAsync(tenantId, (services, ct) => PeriodEndAsync(services, ct), cancellationToken);
            if (endsOn is not { } effectiveUntil)
            {
                continue;
            }

            await context.RunCommandAsync(
                TenancyActions.EntitlementOverride,
                tenantId,
                (services, ct) => GrantAsync(services, tenantId, demo, effectiveUntil, ct),
                cancellationToken);
            inserted++;
        }

        return new SeedOutcome(inserted, Updated: 0, Deactivated: 0);
    }

    /// <summary>
    /// The last day of the tenant's latest period, or null when it already has an override, or has no period that hasn't
    /// ended (an override can't end in the past).
    /// </summary>
    private static async Task<DateOnly?> PeriodEndAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var context = services.GetRequiredService<TenancyDbContext>();
        if (await context.Set<EntitlementOverride>().AnyAsync(cancellationToken))
        {
            return null;
        }

        var today = TenantCalendar.Today(services.GetRequiredService<TimeProvider>(), DemoTenant.TimeZone);
        return await context.Set<SubscriptionPeriod>()
            .Where(p => p.CancelledAt == null && p.EndsOn >= today)
            .OrderByDescending(p => p.StartsOn)
            .Select(p => (DateOnly?)p.EndsOn)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<bool> GrantAsync(
        IServiceProvider services, Guid tenantId, DemoOverride demo, DateOnly effectiveUntil, CancellationToken cancellationToken)
    {
        var request = new GrantOverrideRequest(demo.EntitlementCode, demo.Value, demo.Reason, effectiveUntil);
        await DemoTenantSeeder.ValidateAsync(services, request, cancellationToken);
        DemoTenantSeeder.Succeed(
            await services.GetRequiredService<GrantOverrideHandler>().HandleAsync(tenantId, request, cancellationToken), $"{demo.Slug} override");
        return true;
    }

    private static async Task<bool> AssignAsync(IServiceProvider services, Guid tenantId, DemoSubscription demo, CancellationToken cancellationToken)
    {
        // The demo tenants' zone: the tenant seeder sets it, so it is the stored one.
        var startsOn = TenantCalendar.Today(services.GetRequiredService<TimeProvider>(), DemoTenant.TimeZone);
        var request = new AssignPeriodRequest(TenancyPlans.Standard.Code, demo.Kind, startsOn, demo.EndsOn(startsOn));
        await DemoTenantSeeder.ValidateAsync(services, request, cancellationToken);
        DemoTenantSeeder.Succeed(await services.GetRequiredService<AssignPeriodHandler>().HandleAsync(tenantId, request, cancellationToken), $"{demo.Slug} subscription");
        return true;
    }

    private static string ComputeChecksum()
    {
        var data = JsonSerializer.Serialize(new
        {
            version = _version,
            subscriptions = Subscriptions.Select(s => new { s.Slug, kind = s.Kind.ToString(), s.Months, s.Days }),
            plan = TenancyPlans.Standard.Code,
            overrides = Overrides.Select(o => new { o.Slug, o.EntitlementCode, value = o.Value.ToString(), o.Reason }),
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
    }
}

/// <summary>A demo tenant's override, until its period's last day; the reason is platform text, no personal data.</summary>
internal sealed record DemoOverride(string Slug, string EntitlementCode, EntitlementValue Value, string Reason);

/// <summary>A demo tenant's first period on STANDARD: <see cref="Months"/> months and <see cref="Days"/> days from the seed date.</summary>
internal sealed record DemoSubscription(string Slug, SubscriptionKind Kind, int Months, int Days)
{
    /// <summary>The last day included: six months from 23 October 2026 end on 22 April 2027, 45 days on 6 December 2026.</summary>
    public DateOnly EndsOn(DateOnly startsOn) => startsOn.AddMonths(Months).AddDays(Days - 1);
}
