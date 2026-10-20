using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.Host.Authorization;

/// <summary>Startup check: at most one scope chain provider per resource type (<see cref="ScopeChains"/>).</summary>
public static class ScopeChainRules
{
    /// <summary>Throws when a resource type has more than one provider, listing every such type.</summary>
    public static void EnsureOneScopeChainPerResourceType(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Providers are scoped (they read their module's data); resolving them runs no query.
        using var scope = app.Services.CreateScope();
        var violations = ScopeChains.Validate(scope.ServiceProvider.GetServices<IScopeChainProvider>());
        if (violations.Count > 0)
        {
            throw new InvalidOperationException("Each resource type has at most one scope chain provider:\n" + string.Join("\n", violations));
        }
    }
}
