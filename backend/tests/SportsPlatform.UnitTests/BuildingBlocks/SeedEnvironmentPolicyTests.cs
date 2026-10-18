using SportsPlatform.BuildingBlocks.Persistence.Seeding;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class SeedEnvironmentPolicyTests
{
    public static TheoryData<SeedCategory, bool, string, bool> Cases => new()
    {
        // category, reset, environment, allowed
        { SeedCategory.Reference, false, "Production", true },
        { SeedCategory.Configuration, false, "Production", true },
        { SeedCategory.Demo, false, "Production", false },
        { SeedCategory.Test, false, "Production", false },
        { SeedCategory.Demo, false, "Staging", true },
        { SeedCategory.Test, false, "Staging", true },
        { SeedCategory.Demo, false, "Development", true },
        { SeedCategory.Test, false, "Testing", true },
        { SeedCategory.Demo, false, "production", false }, // Environment names are exact.
        { SeedCategory.Demo, false, "Preview", false },
        { SeedCategory.Reference, true, "Development", true },
        { SeedCategory.Demo, true, "Development", true },
        { SeedCategory.Reference, true, "Testing", false },
        { SeedCategory.Reference, true, "Staging", false },
        { SeedCategory.Reference, true, "Production", false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Allows_sample_data_outside_Production_and_a_reset_only_in_Development(
        SeedCategory category, bool reset, string environment, bool allowed)
    {
        var refusal = SeedEnvironmentPolicy.Refusal(new SeedRequest(category, reset), environment);

        (refusal is null).ShouldBe(allowed);
    }

    [Theory]
    [InlineData(SeedCategory.Reference, new[] { SeedCategory.Reference })]
    [InlineData(SeedCategory.Configuration, new[] { SeedCategory.Reference, SeedCategory.Configuration })]
    [InlineData(SeedCategory.Demo, new[] { SeedCategory.Reference, SeedCategory.Configuration, SeedCategory.Demo })]
    [InlineData(SeedCategory.Test, new[] { SeedCategory.Reference, SeedCategory.Configuration, SeedCategory.Test })]
    public void A_category_brings_its_prerequisites_first(SeedCategory category, SeedCategory[] expected)
    {
        category.WithPrerequisites().ShouldBe(expected);
    }

    [Theory]
    [InlineData(new[] { "--category", "REFERENCE" }, SeedCategory.Reference, false)]
    [InlineData(new[] { "--category", "DEMO", "--reset" }, SeedCategory.Demo, true)]
    [InlineData(new[] { "--reset", "--category", "TEST" }, SeedCategory.Test, true)]
    public void Parses_the_CLI_arguments(string[] args, SeedCategory category, bool reset)
    {
        SeedRequest.TryParse(args, out var request, out var error).ShouldBeTrue(error);

        request.ShouldBe(new SeedRequest(category, reset));
    }

    [Theory]
    [InlineData("")]
    [InlineData("--reset")]
    [InlineData("--category")]
    [InlineData("--category demo")]
    [InlineData("--category 2")]
    [InlineData("--category DEMO --category TEST")]
    [InlineData("--category DEMO --reset --reset")]
    [InlineData("--category DEMO --force")]
    public void Rejects_bad_CLI_arguments(string args)
    {
        SeedRequest.TryParse(args.Split(' ', StringSplitOptions.RemoveEmptyEntries), out var request, out var error).ShouldBeFalse();

        request.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
    }
}
