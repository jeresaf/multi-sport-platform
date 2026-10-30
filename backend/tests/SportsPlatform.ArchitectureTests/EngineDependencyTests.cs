using System.Reflection;
using NetArchTest.Rules;

namespace SportsPlatform.ArchitectureTests;

// S05 D7 and sport-engine.md §4: engines are pure. Of the application they may use BuildingBlocks' results, the
// module's Contracts, each other and the module's errors; of the base library, nothing that reads files, resources,
// the network, the environment, the clock or a random source. A type-level rule can't see Guid.NewGuid() or
// DateTime.UtcNow (Guid and DateTime are plain values), so the engines' source and their Contracts are also read for those calls.
public sealed class EngineDependencyTests
{
    private const string _module = "SportsPlatform.Modules.SportsConfiguration";

    private static readonly string[] _allowedNamespaces =
    [
        "SportsPlatform.BuildingBlocks.Results",
        $"{_module}.Contracts",
        $"{_module}.Engines",
    ];

    private static readonly string[] _allowedTypes = [$"{_module}.SportsConfigurationErrors"];

    private static readonly string[] _impure =
    [
        "System.IO", "System.Net", "System.Random", "System.DateTime", "System.TimeProvider", "System.Environment",
        "System.Threading", "System.Diagnostics.Stopwatch", "System.Diagnostics.Process", "System.Diagnostics.Debug",
        "System.Diagnostics.Trace", "System.Diagnostics.Activity", "System.Security.Cryptography", "System.Console", "System.AppContext",
        "System.Reflection", "System.Resources", "System.GC", "System.Runtime.InteropServices", "System.Activator",
        "Microsoft",
    ];

    // Calls that make a new id or read the clock, written as they appear in source.
    private static readonly string[] _impureCalls =
    [
        "Guid.NewGuid", "Guid.CreateVersion7", "DateTime.Now", "DateTime.UtcNow", "DateTime.Today", "DateTimeOffset.Now",
        "DateTimeOffset.UtcNow", "Environment.", "Random.", "new Random", "Stopwatch.",
    ];

    private static Assembly Module => SolutionAssemblies.Modules.Single(assembly => SolutionAssemblies.Name(assembly) == _module);

    [Fact]
    public void Engines_depend_only_on_results_contracts_and_the_module_errors()
    {
        var engines = Types.InAssembly(Module).That().ResideInNamespace($"{_module}.Engines");
        engines.GetTypes().ShouldNotBeEmpty();

        var result = engines.ShouldNot().HaveDependencyOnAny([.. _impure, .. ForbiddenApplicationTypes()]).GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }

    // The rule must be able to fail: the definition readers use files and YAML, so the same rule refuses them.
    [Fact]
    public void The_rule_refuses_the_definition_readers()
    {
        var result = Types.InAssembly(Module).That().ResideInNamespace($"{_module}.Definitions")
            .ShouldNot().HaveDependencyOnAny([.. _impure, .. ForbiddenApplicationTypes()]).GetResult();

        result.FailingTypeNames.ShouldContain($"{_module}.Definitions.SportDefinitionFiles");
    }

    [Fact]
    public void Engine_source_makes_no_ids_and_reads_no_clock()
    {
        var module = Path.Combine(SolutionAssemblies.BackendDirectory, "src", "Modules", "SportsConfiguration");
        string[] folders =
        [
            Path.Combine(module, _module, "Engines"),
            Path.Combine(module, $"{_module}.Contracts", "Engines"),
        ];
        var files = folders.SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)).ToList();
        folders.ShouldAllBe(folder => files.Any(file => file.StartsWith(folder, StringComparison.Ordinal)));

        files.SelectMany(path => ImpureCalls(File.ReadAllText(path)).Select(call => $"{Path.GetFileName(path)}: {call}")).ShouldBeEmpty();
    }

    [Fact]
    public void The_source_rule_finds_a_new_id_and_the_clock()
    {
        ImpureCalls("var id = Guid.NewGuid(); var at = DateTime.UtcNow;").ShouldBe(["Guid.NewGuid", "DateTime.UtcNow"]);
    }

    private static string[] ImpureCalls(string source) => [.. _impureCalls.Where(call => source.Contains(call, StringComparison.Ordinal))];

    // Every application type outside the allowed namespaces, by full name (a namespace like SportsPlatform.BuildingBlocks
    // can't be listed: the rule matches a namespace and everything under it, so it would also refuse BuildingBlocks.Results).
    private static string[] ForbiddenApplicationTypes() =>
    [
        .. SolutionAssemblies.All
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.FullName is not null && !type.FullName.Contains('<', StringComparison.Ordinal)
                && !_allowedTypes.Contains(type.FullName)
                && !_allowedNamespaces.Any(allowed => type.Namespace == allowed || type.Namespace?.StartsWith(allowed + ".", StringComparison.Ordinal) == true))
            .Select(type => type.FullName!),
    ];
}
