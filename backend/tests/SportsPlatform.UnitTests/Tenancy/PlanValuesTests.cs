using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.Modules.Tenancy.Features.Access;

namespace SportsPlatform.UnitTests.Tenancy;

/// <summary>
/// Which plans include a boolean or level entitlement (S04 D14, "Requires PRO plan"): the active plans, in rank order,
/// whose value lets a tenant use it. STANDARD and PRO are active; the retired STARTER plan isn't.
/// </summary>
public sealed class PlanValuesTests
{
    private static readonly PlanValues _plans = new(
        new Dictionary<string, IReadOnlyDictionary<string, EntitlementValue>>
        {
            ["STARTER"] = new Dictionary<string, EntitlementValue>
            {
                ["CUSTOM_DOMAIN"] = EntitlementValue.Flag(true),
                ["STATISTICS_LEVEL"] = EntitlementValue.Level("FULL"),
            },
            ["STANDARD"] = new Dictionary<string, EntitlementValue>
            {
                ["CUSTOM_DOMAIN"] = EntitlementValue.Flag(false),
                ["STATISTICS_LEVEL"] = EntitlementValue.Level("BASIC"),
            },
            ["PRO"] = new Dictionary<string, EntitlementValue>
            {
                ["CUSTOM_DOMAIN"] = EntitlementValue.Flag(true),
                ["STATISTICS_LEVEL"] = EntitlementValue.Level("FULL"),
                ["MEDIA_LIBRARY"] = EntitlementValue.Flag(false),
            },
        },
        ["STANDARD", "PRO"]);

    [Theory]
    [InlineData("CUSTOM_DOMAIN", new[] { "PRO" })]
    [InlineData("STATISTICS_LEVEL", new[] { "STANDARD", "PRO" })]
    [InlineData("MEDIA_LIBRARY", new string[0])]
    [InlineData("TRANSFERS", new string[0])]
    public void The_plans_allowing_an_entitlement_are_the_active_ones_that_include_it_in_rank_order(string code, string[] expected)
    {
        _plans.PlansAllowing(code).ShouldBe(expected);
    }

    [Fact]
    public void A_level_of_none_does_not_allow()
    {
        var plans = new PlanValues(
            new Dictionary<string, IReadOnlyDictionary<string, EntitlementValue>>
            {
                ["STANDARD"] = new Dictionary<string, EntitlementValue> { ["STATISTICS_LEVEL"] = EntitlementValue.Level(null) },
            },
            ["STANDARD"]);

        plans.PlansAllowing("STATISTICS_LEVEL").ShouldBeEmpty();
    }
}
