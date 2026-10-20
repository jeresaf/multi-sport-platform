using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Modules;

namespace SportsPlatform.Host.Catalogues;

/// <summary>The action catalogue as a service (<see cref="IActionCatalogue"/>), built once from the modules.</summary>
public sealed class DeclaredActions(IEnumerable<IModule> modules) : IActionCatalogue
{
    public IReadOnlyList<ActionDefinition> All { get; } = [.. ActionCatalogue.Build(modules).Select(entry => entry.Action)];
}
