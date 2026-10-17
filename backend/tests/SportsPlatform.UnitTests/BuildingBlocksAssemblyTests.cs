using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.UnitTests;

public sealed class BuildingBlocksAssemblyTests
{
    [Fact]
    public void Assembly_name_matches_the_project()
    {
        typeof(BuildingBlocksAssembly).Assembly.GetName().Name.ShouldBe("SportsPlatform.BuildingBlocks");
    }
}
