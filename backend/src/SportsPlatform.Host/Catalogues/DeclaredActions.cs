using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Modules;

namespace SportsPlatform.Host.Catalogues;

/// <summary>The action catalogue as a service (<see cref="IActionCatalogue"/>), built once from the modules.</summary>
public sealed class DeclaredActions : IActionCatalogue
{
    private readonly Dictionary<string, ActionDefinition> _byCode;

    public DeclaredActions(IEnumerable<IModule> modules)
    {
        All = [.. ActionCatalogue.Build(modules).Select(entry => entry.Action)];
        _byCode = All.ToDictionary(action => action.Code.Value, StringComparer.Ordinal);
    }

    public IReadOnlyList<ActionDefinition> All { get; }

    public ActionDefinition? Find(string code) => _byCode.GetValueOrDefault(code);
}
