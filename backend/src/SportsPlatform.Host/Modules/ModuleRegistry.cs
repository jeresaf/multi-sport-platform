using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.Modules.Platform;
using SportsPlatform.Modules.Tenancy;

namespace SportsPlatform.Host.Modules;

/// <summary>The explicit list of modules the Host runs. Add a module here when its first slice starts.</summary>
public static class ModuleRegistry
{
    public static IReadOnlyList<IModule> All { get; } =
    [
        new PlatformModule(),
        new TenancyModule(),
    ];
}
