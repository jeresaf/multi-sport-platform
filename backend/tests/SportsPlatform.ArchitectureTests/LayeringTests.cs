using NetArchTest.Rules;

namespace SportsPlatform.ArchitectureTests;

public sealed class LayeringTests
{
    private static readonly string[] _frameworks = ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore"];

    [Fact]
    public void Domain_namespaces_do_not_depend_on_EF_Core_or_ASP_NET_Core()
    {
        var domainTypes = Types.InAssemblies([SolutionAssemblies.BuildingBlocks, .. SolutionAssemblies.Modules])
            .That().ResideInNamespaceMatching(@"\.Domain(\.|$)");

        domainTypes.GetTypes().ShouldNotBeEmpty();
        var result = domainTypes.ShouldNot().HaveDependencyOnAny(_frameworks).GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }

    [Fact]
    public void BuildingBlocks_uses_ASP_NET_Core_only_in_its_Modules_namespace()
    {
        var result = Types.InAssembly(SolutionAssemblies.BuildingBlocks)
            .That().DoNotResideInNamespace("SportsPlatform.BuildingBlocks.Modules")
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }

    [Fact]
    public void BuildingBlocks_does_not_use_EF_Core()
    {
        var result = Types.InAssembly(SolutionAssemblies.BuildingBlocks)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }

    // AR-33: cloud SDKs only in infrastructure adapters, which the Host registers.
    [Fact]
    public void Only_the_Host_references_cloud_provider_SDKs()
    {
        var violations = SolutionAssemblies.ApplicationCode
            .SelectMany(assembly => assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(name => name == "Azure" || name.StartsWith("Azure.", StringComparison.Ordinal))
                .Select(name => $"{SolutionAssemblies.Name(assembly)} -> {name}"))
            .ToList();

        violations.ShouldBeEmpty();
    }
}
