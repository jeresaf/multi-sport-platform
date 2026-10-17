using System.Xml.Linq;

namespace SportsPlatform.ArchitectureTests;

// AR-02 and AR-26: modules talk only through Contracts; BuildingBlocks knows no module. Module implementations
// may also use BuildingBlocks.Persistence (their Infrastructure); Contracts may not.
// Assembly metadata lists only references whose types are used, so the project files are checked as well.
public sealed class ModuleBoundaryTests
{
    [Fact]
    public void Module_and_Contracts_project_files_reference_only_allowed_projects()
    {
        var violations = ModuleProjectFiles()
            .SelectMany(project => XDocument.Load(project)
                .Descendants("ProjectReference")
                .Select(reference => Path.GetFileNameWithoutExtension(
                    reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar)))
                .Where(name => !MayReference(Path.GetFileNameWithoutExtension(project), name))
                .Select(name => $"{Path.GetFileNameWithoutExtension(project)} -> {name}"))
            .ToList();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_covers_every_module_project_in_the_source_tree()
    {
        var projects = ModuleProjectFiles()
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Order(StringComparer.Ordinal)
            .ToList();

        var scanned = SolutionAssemblies.All
            .Select(SolutionAssemblies.Name)
            .Where(name => name.StartsWith(SolutionAssemblies.ModulePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        projects.ShouldNotBeEmpty();
        scanned.ShouldBe(projects, "every module project must be referenced (via the Host) so these tests see it");
    }

    [Fact]
    public void Scan_finds_BuildingBlocks_and_the_Platform_module_with_its_Contracts()
    {
        var names = SolutionAssemblies.All.Select(SolutionAssemblies.Name).ToList();

        names.ShouldContain(SolutionAssemblies.BuildingBlocksName);
        names.ShouldContain("SportsPlatform.Modules.Platform");
        names.ShouldContain("SportsPlatform.Modules.Platform.Contracts");
    }

    [Fact]
    public void Modules_reference_only_BuildingBlocks_Persistence_and_Contracts()
    {
        var violations = SolutionAssemblies.Modules
            .SelectMany(module => SolutionAssemblies.SolutionReferences(module)
                .Where(reference => !MayReference(SolutionAssemblies.Name(module), reference))
                .Select(reference => $"{SolutionAssemblies.Name(module)} -> {reference}"))
            .ToList();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Contracts_reference_only_BuildingBlocks_and_other_Contracts()
    {
        var violations = SolutionAssemblies.Contracts
            .SelectMany(contracts => SolutionAssemblies.SolutionReferences(contracts)
                .Where(reference => reference != SolutionAssemblies.BuildingBlocksName
                    && !SolutionAssemblies.IsContracts(reference))
                .Select(reference => $"{SolutionAssemblies.Name(contracts)} -> {reference}"))
            .ToList();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Every_module_has_a_Contracts_project()
    {
        var contracts = SolutionAssemblies.Contracts.Select(SolutionAssemblies.Name).ToHashSet();

        var missing = SolutionAssemblies.Modules
            .Select(SolutionAssemblies.Name)
            .Where(module => !contracts.Contains(SolutionAssemblies.ContractsOf(module)))
            .ToList();

        missing.ShouldBeEmpty();
    }

    [Fact]
    public void BuildingBlocks_references_no_other_solution_assembly()
    {
        SolutionAssemblies.SolutionReferences(SolutionAssemblies.BuildingBlocks).ShouldBeEmpty();
    }

    [Fact]
    public void BuildingBlocks_Persistence_references_only_BuildingBlocks()
    {
        SolutionAssemblies.SolutionReferences(SolutionAssemblies.Persistence)
            .ShouldBe([SolutionAssemblies.BuildingBlocksName]);
    }

    private static bool MayReference(string project, string reference) =>
        reference == SolutionAssemblies.BuildingBlocksName
        || SolutionAssemblies.IsContracts(reference)
        || (reference == SolutionAssemblies.PersistenceName && SolutionAssemblies.IsModule(project));

    private static string[] ModuleProjectFiles() => Directory.GetFiles(
        Path.Combine(SolutionAssemblies.BackendDirectory, "src", "Modules"), "*.csproj", SearchOption.AllDirectories);
}
