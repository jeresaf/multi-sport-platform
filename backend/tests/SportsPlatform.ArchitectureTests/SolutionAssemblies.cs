using System.Reflection;

namespace SportsPlatform.ArchitectureTests;

/// <summary>The application's assemblies, found by scanning the test output folder and grouped by role.</summary>
internal static class SolutionAssemblies
{
    public const string BuildingBlocksName = "SportsPlatform.BuildingBlocks";
    public const string PersistenceName = "SportsPlatform.BuildingBlocks.Persistence";
    public const string HostName = "SportsPlatform.Host";
    public const string ModulePrefix = "SportsPlatform.Modules.";
    public const string ContractsSuffix = ".Contracts";

    public static IReadOnlyList<Assembly> All { get; } = Directory
        .GetFiles(AppContext.BaseDirectory, "SportsPlatform.*.dll")
        .Select(path => Path.GetFileNameWithoutExtension(path))
        .Where(name => name is BuildingBlocksName or PersistenceName or HostName
            || name.StartsWith(ModulePrefix, StringComparison.Ordinal))
        .Order(StringComparer.Ordinal)
        .Select(name => Assembly.Load(new AssemblyName(name)))
        .ToList();

    /// <summary>The <c>backend/</c> folder, found by walking up from the test output to <c>SportsPlatform.slnx</c>.</summary>
    public static string BackendDirectory { get; } = FindBackendDirectory();

    public static Assembly BuildingBlocks => All.Single(a => Name(a) == BuildingBlocksName);

    public static Assembly Persistence => All.Single(a => Name(a) == PersistenceName);

    public static IReadOnlyList<Assembly> Modules => All.Where(IsModule).ToList();

    public static IReadOnlyList<Assembly> Contracts => All.Where(IsContracts).ToList();

    /// <summary>Everything except the Host: the only place cloud adapters may live (AR-33).</summary>
    public static IReadOnlyList<Assembly> ApplicationCode => All.Where(a => Name(a) != HostName).ToList();

    public static string Name(Assembly assembly) => assembly.GetName().Name!;

    public static bool IsContracts(string name) =>
        name.StartsWith(ModulePrefix, StringComparison.Ordinal) && name.EndsWith(ContractsSuffix, StringComparison.Ordinal);

    public static bool IsModule(string name) =>
        name.StartsWith(ModulePrefix, StringComparison.Ordinal) && !IsContracts(name);

    /// <summary>The Contracts assembly that belongs to a module, e.g. Modules.Platform → Modules.Platform.Contracts.</summary>
    public static string ContractsOf(string moduleName) => moduleName + ContractsSuffix;

    /// <summary>Referenced assemblies that are part of this solution (third-party references are ignored).</summary>
    public static IEnumerable<string> SolutionReferences(Assembly assembly) => assembly.GetReferencedAssemblies()
        .Select(a => a.Name!)
        .Where(name => name.StartsWith("SportsPlatform.", StringComparison.Ordinal));

    private static string FindBackendDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SportsPlatform.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("SportsPlatform.slnx not found above the test output folder.");
    }

    private static bool IsModule(Assembly assembly) => IsModule(Name(assembly));

    private static bool IsContracts(Assembly assembly) => IsContracts(Name(assembly));
}
