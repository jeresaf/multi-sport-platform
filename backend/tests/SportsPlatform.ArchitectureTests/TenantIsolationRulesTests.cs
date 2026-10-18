using NetArchTest.Rules;

namespace SportsPlatform.ArchitectureTests;

/// <summary>AR-04: code can't step around the tenant query filter or RLS except in named platform services.</summary>
public sealed class TenantIsolationRulesTests
{
    private const string _platformAuditContext = "SportsPlatform.Modules.Platform.Infrastructure.PlatformAuditDbContext";

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

    // The audit context runs as sp_platform (BYPASSRLS): only the audit features (and the module registering it) use it.
    [Fact]
    public void Only_the_audit_features_use_the_platform_audit_context()
    {
        var users = Types.InAssemblies(SolutionAssemblies.All)
            .That().HaveDependencyOn(_platformAuditContext)
            .GetTypes()
            .Where(type => !(type.FullName ?? string.Empty).StartsWith(_platformAuditContext, StringComparison.Ordinal))
            .ToList();

        users.ShouldContain(type => type.Name == "AuditLog");
        users.Where(type => !(type.Namespace ?? string.Empty).StartsWith("SportsPlatform.Modules.Platform.Features.Audit", StringComparison.Ordinal)
                && type.FullName != "SportsPlatform.Modules.Platform.PlatformModule")
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    private static bool IsBuildOutput(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin") || segments.Contains("obj");
    }
}
