namespace SportsPlatform.ArchitectureTests;

// AR-02 and AR-26: modules talk only through Contracts; BuildingBlocks knows no module.
public sealed class ModuleBoundaryTests
{
    [Fact]
    public void Scan_finds_BuildingBlocks_and_the_Platform_module_with_its_Contracts()
    {
        var names = SolutionAssemblies.All.Select(SolutionAssemblies.Name).ToList();

        names.ShouldContain(SolutionAssemblies.BuildingBlocksName);
        names.ShouldContain("SportsPlatform.Modules.Platform");
        names.ShouldContain("SportsPlatform.Modules.Platform.Contracts");
    }

    [Fact]
    public void Modules_reference_only_BuildingBlocks_and_Contracts()
    {
        var violations = SolutionAssemblies.Modules
            .SelectMany(module => SolutionAssemblies.SolutionReferences(module)
                .Where(reference => reference != SolutionAssemblies.BuildingBlocksName
                    && !SolutionAssemblies.IsContracts(reference))
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
}
