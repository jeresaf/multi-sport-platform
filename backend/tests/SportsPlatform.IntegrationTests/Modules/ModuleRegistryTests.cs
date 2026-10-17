using SportsPlatform.Host.Modules;

namespace SportsPlatform.IntegrationTests.Modules;

public sealed class ModuleRegistryTests
{
    [Fact]
    public void Every_module_assembly_in_the_build_is_registered_in_the_Host()
    {
        var built = Directory.GetFiles(AppContext.BaseDirectory, "SportsPlatform.Modules.*.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !name!.EndsWith(".Contracts", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        var registered = ModuleRegistry.All
            .Select(module => module.GetType().Assembly.GetName().Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        built.ShouldNotBeEmpty();
        registered.ShouldBe(built);
    }

    [Fact]
    public void Module_names_and_schemas_are_unique_and_schemas_are_snake_case()
    {
        ModuleRegistry.All.Select(m => m.Name).ShouldBeUnique();
        ModuleRegistry.All.Select(m => m.Schema).ShouldBeUnique();
        foreach (var module in ModuleRegistry.All)
        {
            module.Schema.ShouldMatch("^[a-z][a-z0-9_]*$");
        }
    }
}
