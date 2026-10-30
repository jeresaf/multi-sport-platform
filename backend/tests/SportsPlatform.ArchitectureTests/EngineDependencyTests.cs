using System.Reflection;
using NetArchTest.Rules;

namespace SportsPlatform.ArchitectureTests;

// S05 D7 and sport-engine.md §4: engines are pure. They may use BuildingBlocks' results, the module's Contracts, each
// other and the module's errors; nothing that reads files, the network, the clock or a random source.
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
        "System.Threading", "System.Diagnostics.Stopwatch", "System.Diagnostics.Process", "System.Security.Cryptography",
        "Microsoft",
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

    // Every application type outside the allowed namespaces, by full name (a namespace like SportsPlatform.BuildingBlocks
    // can't be listed: the rule matches by prefix, so it would also refuse BuildingBlocks.Results).
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
