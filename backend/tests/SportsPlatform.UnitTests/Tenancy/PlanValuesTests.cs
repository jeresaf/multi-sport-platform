using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.Modules.Tenancy.Features.Access;

namespace SportsPlatform.UnitTests.Tenancy;

/// <summary>
/// Which plans include a boolean or level entitlement (S04 D14, "Requires PRO plan"): the active plans, in rank order,
/// whose value lets a tenant use it, as step 2 decides. STANDARD and PRO are active; the retired STARTER plan isn't.
/// </summary>
public sealed class PlanValuesTests
{
    private static readonly EntitlementDefinition _customDomain = new(
        "CUSTOM_DOMAIN", "Custom domain", "Serve the public site on the tenant's own domain.", EntitlementType.Boolean, "Tenancy", "S04");

    private static readonly EntitlementDefinition _statisticsLevel = new(
        "STATISTICS_LEVEL", "Statistics", "How much statistics the public site shows.", EntitlementType.Level, "Statistics", "S04",
        levels: ["BASIC", "FULL"]);

    private static readonly EntitlementDefinition _mediaLibrary = new(
        "MEDIA_LIBRARY", "Media library", "Photos and videos on the public site.", EntitlementType.Boolean, "Media", "S04");

    private static readonly EntitlementDefinition _transfers = new(
        "TRANSFERS", "Transfers", "Move people between clubs.", EntitlementType.Boolean, "Registry", "S04");

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

    public static TheoryData<EntitlementDefinition, string[]> Cases => new()
    {
        { _customDomain, ["PRO"] },
        { _statisticsLevel, ["STANDARD", "PRO"] },
        { _mediaLibrary, [] },
        { _transfers, [] },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_plans_allowing_an_entitlement_are_the_active_ones_that_include_it_in_rank_order(EntitlementDefinition definition, string[] expected)
    {
        _plans.PlansAllowing(definition).ShouldBe(expected);
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

        plans.PlansAllowing(_statisticsLevel).ShouldBeEmpty();
    }

    // As step 2 (EffectiveEntitlements.Of): a stored value the definition doesn't accept counts as the default (#70 R1).
    [Fact]
    public void A_stored_value_the_definition_does_not_accept_does_not_allow()
    {
        var plans = new PlanValues(
            new Dictionary<string, IReadOnlyDictionary<string, EntitlementValue>>
            {
                ["STANDARD"] = new Dictionary<string, EntitlementValue> { ["STATISTICS_LEVEL"] = EntitlementValue.Level("ADVANCED") },
                ["PRO"] = new Dictionary<string, EntitlementValue> { ["CUSTOM_DOMAIN"] = EntitlementValue.Quota(3) },
            },
            ["STANDARD", "PRO"]);

        plans.PlansAllowing(_statisticsLevel).ShouldBeEmpty();
        plans.PlansAllowing(_customDomain).ShouldBeEmpty();
    }
}
