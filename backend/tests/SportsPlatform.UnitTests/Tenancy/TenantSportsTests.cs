using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Domain.Sports;

namespace SportsPlatform.UnitTests.Tenancy;

/// <summary>
/// A tenant's enabled sport variants (S05 D12, D13): one open enablement per variant, a disable closes it, enabling again
/// adds a new one, and the quota counts distinct sports. The variants are made up: generic code never names a real one.
/// </summary>
public sealed class TenantSportsTests
{
    private const string _ballFull = "TESTBALL_FULL";
    private const string _ballShort = "TESTBALL_SHORT";
    private const string _hoop = "TESTHOOP";
    private static readonly DateTimeOffset _now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly UserActor _admin = new(Guid.CreateVersion7());

    private static readonly Dictionary<string, string> _sports = new(StringComparer.Ordinal)
    {
        [_ballFull] = "TESTBALL",
        [_ballShort] = "TESTBALL",
        [_hoop] = "TESTHOOP",
    };

    [Fact]
    public void Enabling_adds_an_open_enablement_by_the_actor()
    {
        var sports = new TenantSports([]);

        var enabled = sports.Enable(_ballFull, _admin, _now);

        enabled.IsSuccess.ShouldBeTrue();
        (enabled.Value.Variant, enabled.Value.EnabledBy, enabled.Value.EnabledAt, enabled.Value.IsEnabled).ShouldBe((_ballFull, _admin.ActorId, _now, true));
        sports.Enabled.ShouldBe([enabled.Value]);
    }

    [Fact]
    public void Enabling_an_enabled_variant_is_refused()
    {
        var sports = new TenantSports([]);
        sports.Enable(_ballFull, _admin, _now);

        sports.Enable(_ballFull, _admin, _now).Error.ShouldBe(TenancyErrors.SportAlreadyEnabled);
        sports.Enabled.Count.ShouldBe(1);
    }

    [Fact]
    public void Disabling_closes_the_enablement_with_the_reason_and_enabling_again_adds_a_new_one()
    {
        var sports = new TenantSports([]);
        var first = sports.Enable(_ballFull, _admin, _now).Value;

        var disabled = sports.Disable(_ballFull, "  Season over  ", _admin, _now.AddDays(1));

        disabled.Value.ShouldBeSameAs(first);
        (first.IsEnabled, first.DisabledAt, first.DisabledBy, first.DisableReason).ShouldBe((false, _now.AddDays(1), _admin.ActorId, "Season over"));
        sports.Enabled.ShouldBeEmpty();
        var second = sports.Enable(_ballFull, _admin, _now.AddDays(2)).Value;
        second.Id.ShouldNotBe(first.Id);
        sports.Enabled.ShouldBe([second]);
    }

    [Fact]
    public void Disabling_a_variant_that_isnt_enabled_is_refused()
    {
        var sports = new TenantSports([]);

        sports.Disable(_ballFull, "Season over", _admin, _now).Error.ShouldBe(TenancyErrors.SportNotEnabled);
    }

    [Fact]
    public void A_disabled_row_is_not_enabled_and_is_closed_once()
    {
        var row = TenantSport.Enable(_hoop, _admin, _now);
        row.Disable("Season over", _admin, _now);

        new TenantSports([row]).Enabled.ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(() => row.Disable("Again", _admin, _now));
    }

    [Fact]
    public void The_quota_counts_two_variants_of_one_sport_once()
    {
        var sports = new TenantSports([]);
        sports.Enable(_ballFull, _admin, _now);

        sports.SportsCounted(SportOf).ShouldBe(1);
        sports.AddsSport("TESTBALL", SportOf).ShouldBeFalse();
        sports.AddsSport("TESTHOOP", SportOf).ShouldBeTrue();

        sports.Enable(_ballShort, _admin, _now);
        sports.SportsCounted(SportOf).ShouldBe(1);

        sports.Enable(_hoop, _admin, _now);
        sports.SportsCounted(SportOf).ShouldBe(2);
    }

    [Fact]
    public void A_variant_that_is_no_longer_published_counts_as_its_own_sport()
    {
        var sports = new TenantSports([]);
        sports.Enable("TESTGONE", _admin, _now);

        sports.SportsCounted(SportOf).ShouldBe(1);
    }

    private static string? SportOf(string variant) => _sports.GetValueOrDefault(variant);
}
