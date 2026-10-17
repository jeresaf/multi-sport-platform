using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.ArchitectureTests;

// Placeholder until S01 adds NetArchTest-based rules (AR-02, AR-26).
public sealed class BuildingBlocksTests
{
    [Fact]
    public void BuildingBlocks_does_not_reference_AspNetCore_or_EntityFramework()
    {
        var references = typeof(BuildingBlocksAssembly).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty);

        references.ShouldNotContain(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }
}
