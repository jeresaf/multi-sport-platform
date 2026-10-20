namespace SportsPlatform.BuildingBlocks.Actions;

/// <summary>
/// The scope chain providers of the modules, one per resource type (the Host refuses to start with two for one type,
/// <see cref="Validate"/>). A resource whose type has no provider is its own whole chain.
/// </summary>
public sealed class ScopeChains(IEnumerable<IScopeChainProvider> providers)
{
    private readonly IReadOnlyList<IScopeChainProvider> _providers = [.. providers];

    /// <summary>The resource and the scopes above it in the request's tenant.</summary>
    public async Task<IReadOnlySet<ScopeRef>> ChainAsync(ScopeRef resource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var chain = new HashSet<ScopeRef> { resource };
        if (_providers.SingleOrDefault(provider => provider.ResourceType == resource.Type) is { } provider)
        {
            chain.UnionWith(await provider.GetAncestorsAsync(resource.Id, cancellationToken));
        }

        return chain;
    }

    /// <summary>One violation per resource type that has more than one provider.</summary>
    public static IReadOnlyList<string> Validate(IEnumerable<IScopeChainProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        return [.. providers
            .GroupBy(provider => provider.ResourceType)
            .Where(group => group.Count() > 1)
            .Select(group => $"Resource type {group.Key} has {group.Count()} scope chain providers: " +
                string.Join(", ", group.Select(provider => provider.GetType().Name)) + ".")];
    }
}
