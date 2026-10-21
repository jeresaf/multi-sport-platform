using SportsPlatform.BuildingBlocks.Entitlements;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class EntitlementDefinitionTests
{
    [Fact]
    public void Level_entitlement_keeps_its_levels_in_order()
    {
        var reports = new EntitlementDefinition(
            "REPORTS", "Reports", "Report formats.", EntitlementType.Level, "Insights", "S04", levels: ["BASIC", "ADVANCED"]);

        reports.Code.ShouldBe("REPORTS");
        reports.Levels.ShouldBe(["BASIC", "ADVANCED"]);
        reports.Unit.ShouldBeNull();
        reports.EnforcedBy.ShouldBe("Insights");
        reports.Default.ShouldBe(EntitlementValue.Level(null));
        reports.Accepts(EntitlementValue.Level("ADVANCED")).ShouldBeTrue();
        reports.Accepts(EntitlementValue.Level(null)).ShouldBeTrue();
        reports.Accepts(EntitlementValue.Level("FULL")).ShouldBeFalse();
        reports.Accepts(EntitlementValue.Flag(true)).ShouldBeFalse();
    }

    [Fact]
    public void Defaults_are_off_zero_and_no_level()
    {
        Boolean().Default.ShouldBe(EntitlementValue.Flag(false));
        Quota().Default.ShouldBe(EntitlementValue.Quota(0));
        Quota().Accepts(EntitlementValue.UnlimitedQuota).ShouldBeTrue();
        Quota().Accepts(EntitlementValue.Flag(true)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("max_teams")]
    [InlineData("MAX-TEAMS")]
    [InlineData("_MAX")]
    [InlineData("MAX__TEAMS")]
    [InlineData("")]
    public void Code_must_be_upper_snake_case(string code)
    {
        Should.Throw<ArgumentException>(() =>
            new EntitlementDefinition(code, "Teams", "Teams.", EntitlementType.Quota, "Registry", "S04", unit: "teams"));
    }

    [Fact]
    public void A_quota_needs_a_unit_and_only_a_quota_has_one()
    {
        Should.Throw<ArgumentException>(() => new EntitlementDefinition("MAX_TEAMS", "Teams", "Teams.", EntitlementType.Quota, "Registry", "S04"))
            .Message.ShouldContain("unit");
        Should.Throw<ArgumentException>(() =>
            new EntitlementDefinition("TRANSFERS", "Transfers", "Transfers.", EntitlementType.Boolean, "Team Operations", "S04", unit: "transfers"));
    }

    [Fact]
    public void A_level_entitlement_needs_distinct_levels_and_only_it_has_them()
    {
        Should.Throw<ArgumentException>(() => new EntitlementDefinition("REPORTS", "Reports", "Reports.", EntitlementType.Level, "Insights", "S04"))
            .Message.ShouldContain("levels");
        Should.Throw<ArgumentException>(() =>
            new EntitlementDefinition("REPORTS", "Reports", "Reports.", EntitlementType.Level, "Insights", "S04", levels: ["BASIC", "BASIC"]));
        Should.Throw<ArgumentException>(() =>
            new EntitlementDefinition("REPORTS", "Reports", "Reports.", EntitlementType.Level, "Insights", "S04", levels: ["basic"]));
        Should.Throw<ArgumentException>(() =>
            new EntitlementDefinition("TRANSFERS", "Transfers", "Transfers.", EntitlementType.Boolean, "Team Operations", "S04", levels: ["ON"]));
    }

    [Fact]
    public void Values_are_written_as_the_catalogue_shows_them()
    {
        EntitlementValue.Flag(true).ToString().ShouldBe("yes");
        EntitlementValue.Flag(false).ToString().ShouldBe("no");
        EntitlementValue.Quota(2_000).ToString().ShouldBe("2000");
        EntitlementValue.UnlimitedQuota.ToString().ShouldBe("unlimited");
        EntitlementValue.UnlimitedQuota.Unlimited.ShouldBeTrue();
        EntitlementValue.Quota(0).Unlimited.ShouldBeFalse();
        EntitlementValue.Level("FULL").ToString().ShouldBe("FULL");
        EntitlementValue.Level(null).ToString().ShouldBe("none");
    }

    [Fact]
    public void A_negative_quota_or_a_badly_formed_level_is_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => EntitlementValue.Quota(-1));
        Should.Throw<ArgumentException>(() => EntitlementValue.Level("full"));
    }

    private static EntitlementDefinition Boolean() =>
        new("TRANSFERS", "Transfers", "Transfers.", EntitlementType.Boolean, "Team Operations", "S04");

    private static EntitlementDefinition Quota() =>
        new("MAX_TEAMS", "Teams", "Teams.", EntitlementType.Quota, "Registry", "S04", unit: "teams");
}
