using System.Reflection;
using NetArchTest.Rules;

namespace SportsPlatform.ArchitectureTests;

// S05 D10 and T3's acceptance: the fixture generators are pure and depend on no other module. Like the engines (see
// EngineDependencyTests), they may use BuildingBlocks' results, the module's Contracts, each other and the module's
// errors, plus the module's canonical JSON hash and its parameter checks (shared with the ruleset resolver); nothing that reads files, the clock or a random source, and no
// System.Random: the draw's PRNG is the module's own, so a stored seed gives the same draw on every .NET version.
public sealed class GeneratorDependencyTests
{
    private const string _module = "SportsPlatform.Modules.SportsConfiguration";

    private static readonly string[] _allowedNamespaces =
    [
        "SportsPlatform.BuildingBlocks.Results",
        $"{_module}.Contracts",
        $"{_module}.Generators",
        $"{_module}.Hashing",
        $"{_module}.Parameters",
    ];

    private static readonly string[] _allowedTypes = [$"{_module}.SportsConfigurationErrors"];

    private static Assembly Module => SolutionAssemblies.Modules.Single(assembly => SolutionAssemblies.Name(assembly) == _module);

    [Fact]
    public void Generators_depend_only_on_results_contracts_the_hash_and_the_module_errors()
    {
        var generators = Types.InAssembly(Module).That().ResideInNamespace($"{_module}.Generators");
        generators.GetTypes().ShouldNotBeEmpty();

        var result = generators.ShouldNot()
            .HaveDependencyOnAny([.. EngineDependencyTests.Impure, .. EngineDependencyTests.ForbiddenApplicationTypes(_allowedNamespaces, _allowedTypes)])
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }

    // The rule must be able to fail: the definition readers use files, YAML and the seeders' persistence.
    [Fact]
    public void The_rule_refuses_the_definition_readers()
    {
        var result = Types.InAssembly(Module).That().ResideInNamespace($"{_module}.Definitions")
            .ShouldNot()
            .HaveDependencyOnAny([.. EngineDependencyTests.Impure, .. EngineDependencyTests.ForbiddenApplicationTypes(_allowedNamespaces, _allowedTypes)])
            .GetResult();

        result.FailingTypeNames.ShouldContain($"{_module}.Definitions.FormatDefinitionFiles");
    }

    [Fact]
    public void Generator_source_makes_no_ids_and_reads_no_clock()
    {
        var module = Path.Combine(SolutionAssemblies.BackendDirectory, "src", "Modules", "SportsConfiguration");
        string[] folders =
        [
            Path.Combine(module, _module, "Generators"),
            Path.Combine(module, $"{_module}.Contracts", "Formats", "Generation"),
        ];
        var files = folders.SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)).ToList();
        folders.ShouldAllBe(folder => files.Any(file => file.StartsWith(folder, StringComparison.Ordinal)));

        files.SelectMany(path => EngineDependencyTests.ImpureCalls(File.ReadAllText(path)).Select(call => $"{Path.GetFileName(path)}: {call}")).ShouldBeEmpty();
    }
}
