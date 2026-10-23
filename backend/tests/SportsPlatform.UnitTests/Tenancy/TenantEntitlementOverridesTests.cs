using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;

namespace SportsPlatform.UnitTests.Tenancy;

/// <summary>A tenant's entitlement overrides (S04 D5): typed values, one in place per entitlement, history kept, and the last day.</summary>
public sealed class TenantEntitlementOverridesTests
{
    private static readonly DateOnly _today = new(2026, 10, 23);
    private static readonly DateTimeOffset _now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly UserActor _admin = new(Guid.CreateVersion7());

    public static TheoryData<EntitlementDefinition, EntitlementValue> AcceptedValues => new()
    {
        { TenancyEntitlements.MaxStaffUsers, EntitlementValue.Quota(5) },
        { TenancyEntitlements.MaxStaffUsers, EntitlementValue.Quota(0) },
        { TenancyEntitlements.MaxStaffUsers, EntitlementValue.UnlimitedQuota },
        { TenancyEntitlements.CustomDomain, EntitlementValue.Flag(true) },
        { TenancyEntitlements.LiveScoring, EntitlementValue.Flag(false) },
        { TenancyEntitlements.Reports, EntitlementValue.Level("ADVANCED") },
        { TenancyEntitlements.Reports, EntitlementValue.Level(null) },
    };

    public static TheoryData<EntitlementDefinition, EntitlementValue> RefusedValues => new()
    {
        { TenancyEntitlements.MaxStaffUsers, EntitlementValue.Flag(true) },
        { TenancyEntitlements.CustomDomain, EntitlementValue.Quota(1) },
        { TenancyEntitlements.Reports, EntitlementValue.Level("FULL") },
        { TenancyEntitlements.Branding, EntitlementValue.Flag(true) },
    };

    [Theory]
    [MemberData(nameof(AcceptedValues))]
    public void A_value_of_the_entitlements_type_is_granted(EntitlementDefinition definition, EntitlementValue value)
    {
        var overrides = new TenantEntitlementOverrides([]);

        var granted = overrides.Grant(definition, value, "Pilot agreement", effectiveUntil: null, _admin, _now);

        granted.IsSuccess.ShouldBeTrue();
        var entitlementOverride = granted.Value.Granted;
        (entitlementOverride.EntitlementCode, entitlementOverride.Value, entitlementOverride.Reason).ShouldBe((definition.Code, value, "Pilot agreement"));
        granted.Value.Replaced.ShouldBeNull();
        overrides.Overrides.ShouldBe([entitlementOverride]);
    }

    [Theory]
    [MemberData(nameof(RefusedValues))]
    public void A_value_of_another_type_or_level_is_refused(EntitlementDefinition definition, EntitlementValue value)
    {
        var overrides = new TenantEntitlementOverrides([]);

        overrides.Grant(definition, value, "Pilot agreement", effectiveUntil: null, _admin, _now).Error.ShouldBe(TenancyErrors.OverrideInvalidValue);
        overrides.Overrides.ShouldBeEmpty();
    }

    [Fact]
    public void A_new_grant_replaces_the_override_in_place_and_keeps_it()
    {
        var overrides = new TenantEntitlementOverrides([]);
        var first = overrides.Grant(TenancyEntitlements.MaxStaffUsers, EntitlementValue.Quota(5), "Pilot agreement", null, _admin, _now).Value.Granted;
        var other = overrides.Grant(TenancyEntitlements.MaxTeams, EntitlementValue.Quota(150), "Pilot agreement", null, _admin, _now).Value.Granted;

        var second = overrides.Grant(TenancyEntitlements.MaxStaffUsers, EntitlementValue.Quota(8), "Two more scorers", null, _admin, _now);

        second.Value.Replaced.ShouldBeSameAs(first);
        (first.IsRemoved, first.RemovedAt, first.RemovedBy, first.RemoveReason)
            .ShouldBe((true, _now, _admin.ActorId, TenantEntitlementOverrides.ReplacedReason));
        other.IsRemoved.ShouldBeFalse();
        overrides.Overrides.Count.ShouldBe(3);
        overrides.InEffectOn(_today).Values.ShouldBe([other, second.Value.Granted], ignoreOrder: true);
    }

    [Fact]
    public void An_override_applies_until_its_last_day_included()
    {
        var eventEnd = new DateOnly(2026, 12, 6);
        var overrides = new TenantEntitlementOverrides([]);
        var eventPass = overrides.Grant(TenancyEntitlements.MaxActiveEditions, EntitlementValue.Quota(1), "Event pass", eventEnd, _admin, _now).Value.Granted;

        eventPass.IsInEffectOn(eventEnd).ShouldBeTrue();
        overrides.InEffectOn(eventEnd).ShouldContainKey("MAX_ACTIVE_EDITIONS");
        eventPass.IsInEffectOn(eventEnd.AddDays(1)).ShouldBeFalse();
        overrides.InEffectOn(eventEnd.AddDays(1)).ShouldBeEmpty();
    }

    [Fact]
    public void A_removed_override_no_longer_applies()
    {
        var overrides = new TenantEntitlementOverrides([]);
        var granted = overrides.Grant(TenancyEntitlements.MaxStaffUsers, EntitlementValue.Quota(5), "Pilot agreement", null, _admin, _now).Value.Granted;

        var removed = overrides.Remove(granted.Id, "Pilot ended", _admin, _now);

        removed.Value.ShouldBeSameAs(granted);
        (granted.RemovedAt, granted.RemovedBy, granted.RemoveReason).ShouldBe((_now, _admin.ActorId, "Pilot ended"));
        granted.IsInEffectOn(_today).ShouldBeFalse();
        overrides.InEffectOn(_today).ShouldBeEmpty();
    }

    [Fact]
    public void Removing_an_unknown_or_removed_override_is_not_found()
    {
        var overrides = new TenantEntitlementOverrides([]);
        var granted = overrides.Grant(TenancyEntitlements.MaxStaffUsers, EntitlementValue.Quota(5), "Pilot agreement", null, _admin, _now).Value.Granted;
        overrides.Remove(granted.Id, "Pilot ended", _admin, _now);

        overrides.Remove(granted.Id, "Again", _admin, _now).Error.ShouldBe(TenancyErrors.OverrideNotFound);
        overrides.Remove(Guid.CreateVersion7(), "Unknown", _admin, _now).Error.ShouldBe(TenancyErrors.OverrideNotFound);
        granted.RemoveReason.ShouldBe("Pilot ended");
    }

    [Fact]
    public void Unremoved_overrides_come_first()
    {
        var overrides = new TenantEntitlementOverrides([]);
        var first = overrides.Grant(TenancyEntitlements.MaxStaffUsers, EntitlementValue.Quota(5), "Pilot agreement", null, _admin, _now).Value.Granted;
        var second = overrides.Grant(TenancyEntitlements.MaxStaffUsers, EntitlementValue.Quota(8), "Two more scorers", null, _admin, _now).Value.Granted;

        overrides.Overrides.ShouldBe([second, first]);
    }

    [Fact]
    public void Reasons_are_trimmed()
    {
        var entitlementOverride = EntitlementOverride.Create("MAX_TEAMS", EntitlementValue.Quota(150), "  Pilot agreement  ", null);

        entitlementOverride.Reason.ShouldBe("Pilot agreement");
        Should.Throw<InvalidOperationException>(() =>
        {
            entitlementOverride.Remove(" Pilot ended ", SystemActor.Instance, _now);
            entitlementOverride.Remove("Again", SystemActor.Instance, _now);
        });
        entitlementOverride.RemoveReason.ShouldBe("Pilot ended");
    }
}
