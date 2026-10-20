using NetArchTest.Rules;
using SportsPlatform.BuildingBlocks.Persistence;

namespace SportsPlatform.ArchitectureTests;

/// <summary>AR-04: code can't step around the tenant query filter or RLS except in named platform services.</summary>
public sealed class TenantIsolationRulesTests
{
    /// <summary>Source files (relative to <c>backend/src</c>) of the reviewed platform services that may ignore the filter.</summary>
    private static readonly HashSet<string> _mayIgnoreQueryFilters = [];

    [Fact]
    public void No_code_ignores_query_filters_outside_named_platform_services()
    {
        var source = Path.Combine(SolutionAssemblies.BackendDirectory, "src");
        var files = Directory.GetFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .ToList();

        files.ShouldNotBeEmpty();
        files.Where(path => File.ReadAllText(path).Contains("IgnoreQueryFilters(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(source, path).Replace('\\', '/'))
            .Where(path => !_mayIgnoreQueryFilters.Contains(path))
            .ShouldBeEmpty();
    }

    // Platform service contexts run as sp_platform (BYPASSRLS): each is named here with the only namespaces (and the
    // module class that registers it) that may use it (S03 D8, #33 N2).
    private static readonly Dictionary<string, string[]> _platformServiceUsers = new(StringComparer.Ordinal)
    {
        ["SportsPlatform.Modules.Platform.Infrastructure.PlatformAuditDbContext"] =
            ["SportsPlatform.Modules.Platform.Features.Audit.", "SportsPlatform.Modules.Platform.PlatformModule"],
        ["SportsPlatform.Modules.Identity.Infrastructure.Directory.IdentityDirectoryDbContext"] =
            ["SportsPlatform.Modules.Identity.Features.Directory.", "SportsPlatform.Modules.Identity.IdentityModule"],
    };

    public static TheoryData<string> PlatformServiceContexts { get; } = [.. _platformServiceUsers.Keys];

    [Fact]
    public void Every_platform_service_context_is_named_with_its_users()
    {
        var contexts = Types.InAssemblies(SolutionAssemblies.All)
            .That().Inherit(typeof(PlatformServiceDbContext))
            .GetTypes()
            .Select(type => type.FullName)
            .ToList();

        contexts.ShouldBe(_platformServiceUsers.Keys, ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(PlatformServiceContexts))]
    public void Only_the_named_features_use_a_platform_service_context(string context)
    {
        var users = Types.InAssemblies(SolutionAssemblies.All)
            .That().HaveDependencyOn(context)
            .GetTypes()
            .Select(type => type.FullName ?? string.Empty)
            .Where(name => !name.StartsWith(context, StringComparison.Ordinal))
            .ToList();

        users.ShouldNotBeEmpty();
        users.Where(name => !_platformServiceUsers[context].Any(allowed => name.StartsWith(allowed, StringComparison.Ordinal)))
            .ShouldBeEmpty();
    }

    private static bool IsBuildOutput(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin") || segments.Contains("obj");
    }
}
