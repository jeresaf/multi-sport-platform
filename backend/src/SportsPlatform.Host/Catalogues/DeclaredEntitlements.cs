using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Modules;

namespace SportsPlatform.Host.Catalogues;

/// <summary>The entitlement catalogue as a service (<see cref="IEntitlementCatalogue"/>), built once from the modules.</summary>
public sealed class DeclaredEntitlements : IEntitlementCatalogue
{
    private readonly Dictionary<string, EntitlementDefinition> _byCode;

    public DeclaredEntitlements(IEnumerable<IModule> modules)
        : this(EntitlementCatalogue.Build(modules).Select(entry => entry.Entitlement))
    {
    }

    /// <summary>A catalogue of exactly <paramref name="entitlements"/> (tests replace the declared one with it).</summary>
    public DeclaredEntitlements(IEnumerable<EntitlementDefinition> entitlements)
    {
        ArgumentNullException.ThrowIfNull(entitlements);
        All = [.. entitlements.OrderBy(entitlement => entitlement.Code, StringComparer.Ordinal)];
        _byCode = All.ToDictionary(entitlement => entitlement.Code, StringComparer.Ordinal);
    }

    public IReadOnlyList<EntitlementDefinition> All { get; }

    public EntitlementDefinition? Find(string code) => _byCode.GetValueOrDefault(code);
}
