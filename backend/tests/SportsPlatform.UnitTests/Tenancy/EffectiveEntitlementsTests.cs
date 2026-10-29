using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;

namespace SportsPlatform.UnitTests.Tenancy;

/// <summary>Effective entitlements (S04 D6): default, then the plan, then an override in effect; using a value; quotas.</summary>
public sealed class EffectiveEntitlementsTests
{
    private static readonly DateOnly _today = new(2026, 10, 29);
    private static readonly DateTimeOffset _now = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, EntitlementValue> _standard = new(StringComparer.Ordinal)
    {
        [TenancyEntitlements.MaxStaffUsers.Code] = EntitlementValue.Quota(10),
        [TenancyEntitlements.CustomDomain.Code] = EntitlementValue.Flag(false),
        [TenancyEntitlements.Reports.Code] = EntitlementValue.Level("BASIC"),
    };

    [Fact]
    public void Without_a_period_every_entitlement_has_its_default()
    {
        var effective = new EffectiveEntitlements(planCode: null, _standard, Overrides());

        effective.Of(TenancyEntitlements.MaxStaffUsers).ShouldBe(
            new EffectiveEntitlement("MAX_STAFF_USERS", EntitlementValue.Quota(0), EntitlementSource.Default, null));
        effective.Of(TenancyEntitlements.CustomDomain).Value.ShouldBe(EntitlementValue.Flag(false));
        effective.Of(TenancyEntitlements.Reports).Value.ShouldBe(EntitlementValue.Level(null));
    }

    [Fact]
    public void The_plan_gives_its_values_and_the_default_where_it_has_none()
    {
        var effective = new EffectiveEntitlements("STANDARD", _standard, Overrides());

        effective.Of(TenancyEntitlements.MaxStaffUsers).ShouldBe(
            new EffectiveEntitlement("MAX_STAFF_USERS", EntitlementValue.Quota(10), EntitlementSource.Plan, "STANDARD"));
        effective.Of(TenancyEntitlements.Reports).Value.ShouldBe(EntitlementValue.Level("BASIC"));
        effective.Of(TenancyEntitlements.LiveScoring).ShouldBe(
            new EffectiveEntitlement("LIVE_SCORING", EntitlementValue.Flag(false), EntitlementSource.Default, "STANDARD"));
    }

    [Fact]
    public void An_override_in_effect_replaces_the_plans_value()
    {
        var effective = new EffectiveEntitlements(
            "STANDARD", _standard, Overrides((TenancyEntitlements.MaxStaffUsers, EntitlementValue.Quota(5), _today)));

        effective.Of(TenancyEntitlements.MaxStaffUsers).ShouldBe(
            new EffectiveEntitlement("MAX_STAFF_USERS", EntitlementValue.Quota(5), EntitlementSource.Override, "STANDARD"));
        effective.Of(TenancyEntitlements.CustomDomain).Source.ShouldBe(EntitlementSource.Plan);
    }

    [Fact]
    public void An_override_applies_without_a_period_too()
    {
        var effective = new EffectiveEntitlements(
            planCode: null, _standard, Overrides((TenancyEntitlements.CustomDomain, EntitlementValue.Flag(true), null)));

        effective.Of(TenancyEntitlements.CustomDomain).ShouldBe(
            new EffectiveEntitlement("CUSTOM_DOMAIN", EntitlementValue.Flag(true), EntitlementSource.Override, null));
    }

    [Fact]
    public void An_override_past_its_last_day_or_removed_no_longer_applies()
    {
        var overrides = new TenantEntitlementOverrides([]);
        var actor = new UserActor(Guid.CreateVersion7());
        overrides.Grant(TenancyEntitlements.MaxStaffUsers, EntitlementValue.Quota(5), "Event pass", _today.AddDays(-1), actor, _now)
            .IsSuccess.ShouldBeTrue();
        var removed = overrides.Grant(TenancyEntitlements.CustomDomain, EntitlementValue.Flag(true), "Pilot", null, actor, _now).Value.Granted;
        overrides.Remove(removed.Id, "Pilot ended", actor, _now).IsSuccess.ShouldBeTrue();

        var effective = new EffectiveEntitlements("STANDARD", _standard, overrides.InEffectOn(_today));

        effective.Of(TenancyEntitlements.MaxStaffUsers).ShouldBe(
            new EffectiveEntitlement("MAX_STAFF_USERS", EntitlementValue.Quota(10), EntitlementSource.Plan, "STANDARD"));
        effective.Of(TenancyEntitlements.CustomDomain).Source.ShouldBe(EntitlementSource.Plan);
    }

    [Fact]
    public void A_stored_value_of_another_type_is_ignored()
    {
        var stale = new Dictionary<string, EntitlementValue>(StringComparer.Ordinal)
        {
            [TenancyEntitlements.CustomDomain.Code] = EntitlementValue.Quota(3),
            [TenancyEntitlements.Reports.Code] = EntitlementValue.Level("PREMIUM"),
        };

        var effective = new EffectiveEntitlements("STANDARD", stale, Overrides());

        effective.Of(TenancyEntitlements.CustomDomain).ShouldBe(
            new EffectiveEntitlement("CUSTOM_DOMAIN", EntitlementValue.Flag(false), EntitlementSource.Default, "STANDARD"));
        effective.Of(TenancyEntitlements.Reports).Source.ShouldBe(EntitlementSource.Default);
    }

    // The current period's plan applies whatever its status (owner defaults, plan 2026-10-29): step 1 refuses the writes.
    public static TheoryData<string, string?, EntitlementSource> PlanCases => new()
    {
        { "in effect", "PRO", EntitlementSource.Plan },
        { "expired", "PRO", EntitlementSource.Plan },
        { "cancelled", "PRO", EntitlementSource.Plan },
        { "scheduled only", "PRO", EntitlementSource.Plan },
        { "expired, then scheduled", "PRO", EntitlementSource.Plan },
        { "none", null, EntitlementSource.Default },
    };

    [Theory]
    [MemberData(nameof(PlanCases))]
    public void The_current_periods_plan_gives_the_values(string periods, string? planCode, EntitlementSource source)
    {
        var subscription = new TenantSubscription(periods switch
        {
            "in effect" => [Period("PRO", _today.AddDays(-10), _today.AddDays(100))],
            "expired" => [Period("PRO", _today.AddDays(-100), _today.AddDays(-30))],
            "cancelled" => [Period("PRO", _today.AddDays(-10), _today.AddDays(100), cancelled: true)],
            "scheduled only" => [Period("PRO", _today.AddDays(5), _today.AddDays(100))],
            "expired, then scheduled" =>
                [Period("PRO", _today.AddDays(-100), _today.AddDays(-30)), Period("STANDARD", _today.AddDays(5), _today.AddDays(100))],
            _ => [],
        });
        var pro = new Dictionary<string, EntitlementValue>(StringComparer.Ordinal) { ["CUSTOM_DOMAIN"] = EntitlementValue.Flag(true) };

        var effective = EffectiveEntitlements.On(
            _today, subscription, new TenantEntitlementOverrides([]), code => code == "PRO" ? pro : _standard);

        effective.Of(TenancyEntitlements.CustomDomain).ShouldBe(new EffectiveEntitlement(
            "CUSTOM_DOMAIN", EntitlementValue.Flag(planCode is not null), source, planCode));
    }

    public static TheoryData<EntitlementValue, bool> Uses => new()
    {
        { EntitlementValue.Flag(true), true },
        { EntitlementValue.Flag(false), false },
        { EntitlementValue.Level("BASIC"), true },
        { EntitlementValue.Level(null), false },
    };

    [Theory]
    [MemberData(nameof(Uses))]
    public void A_boolean_that_is_on_or_any_level_is_usable(EntitlementValue value, bool usable) =>
        EffectiveEntitlements.Allows(value).ShouldBe(usable);

    [Fact]
    public void A_quota_is_counted_not_allowed() =>
        Should.Throw<ArgumentException>(() => EffectiveEntitlements.Allows(EntitlementValue.Quota(3)));

    // Limit 10: the 8th item (7 used) reaches 80 %; the 10th (9 used) is the last; with 10 used the next is refused.
    [Theory]
    [InlineData(0, false)]
    [InlineData(6, false)]
    [InlineData(7, true)]
    [InlineData(9, true)]
    public void A_quota_with_room_warns_from_80_percent(int used, bool warning) =>
        EffectiveEntitlements.CheckQuota(EntitlementValue.Quota(10), used).Value.ShouldBe(new QuotaCheck(10, used, warning));

    [Theory]
    [InlineData(10, 10)]
    [InlineData(10, 12)]
    [InlineData(0, 0)]
    public void A_quota_at_or_past_its_limit_refuses_the_next_item(int limit, int used) =>
        EffectiveEntitlements.CheckQuota(EntitlementValue.Quota(limit), used).Error.ShouldBe(TenancyErrors.QuotaExceeded);

    [Fact]
    public void An_unlimited_quota_never_warns_or_refuses() =>
        EffectiveEntitlements.CheckQuota(EntitlementValue.UnlimitedQuota, 100_000).Value.ShouldBe(new QuotaCheck(null, 100_000, false));

    // The usage reads (S04 D15): 4 of 5 is 80 %, so it warns; so does a tenant past a lowered limit; never unlimited.
    [Theory]
    [InlineData(5, 3, false)]
    [InlineData(5, 4, true)]
    [InlineData(5, 7, true)]
    [InlineData(0, 0, true)]
    [InlineData(null, 1_000, false)]
    public void Usage_is_near_the_limit_from_80_percent(int? limit, int used, bool near) =>
        EffectiveEntitlements.IsNearLimit(limit, used).ShouldBe(near);

    [Fact]
    public void Only_a_quota_is_checked() =>
        Should.Throw<ArgumentException>(() => EffectiveEntitlements.CheckQuota(EntitlementValue.Flag(true), 0));

    private static SubscriptionPeriod Period(string planCode, DateOnly startsOn, DateOnly endsOn, bool cancelled = false)
    {
        var period = SubscriptionPeriod.Create(
            planCode, SubscriptionKind.Paid, startsOn, endsOn, SubscriptionPeriod.DefaultExpiringWindowDays, SubscriptionPeriod.DefaultGraceDays);
        if (cancelled)
        {
            period.Cancel("Association closed", SystemActor.Instance, _now);
        }

        return period;
    }

    private static IReadOnlyDictionary<string, EntitlementOverride> Overrides(
        params (EntitlementDefinition Definition, EntitlementValue Value, DateOnly? Until)[] overrides)
    {
        var tenant = new TenantEntitlementOverrides([]);
        foreach (var (definition, value, until) in overrides)
        {
            tenant.Grant(definition, value, "Pilot agreement", until, new UserActor(Guid.CreateVersion7()), _now).IsSuccess.ShouldBeTrue();
        }

        return tenant.InEffectOn(_today);
    }
}
