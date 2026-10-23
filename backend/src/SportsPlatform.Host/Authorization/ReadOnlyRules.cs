using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.Host.Catalogues;

namespace SportsPlatform.Host.Authorization;

/// <summary>
/// Startup check of the read-only flag (S04 D8): a read-only tenant keeps reading, so every tenant action whose code
/// ends in <c>.read</c> is <c>availableWhenReadOnly</c>. A write may set it too; it then checks its own window in the
/// handler.
/// </summary>
public static class ReadOnlyRules
{
    private const string _readSuffix = ".read";

    public static IReadOnlyList<string> Validate(IReadOnlyList<ActionCatalogueEntry> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return [.. actions
            .Select(entry => entry.Action)
            .Where(action => action.Scope == ActionScope.Tenant
                && action.Code.Value.EndsWith(_readSuffix, StringComparison.Ordinal)
                && !action.AvailableWhenReadOnly)
            .Select(action => $"Tenant read '{action.Code}' must be availableWhenReadOnly: a read-only tenant keeps reading.")];
    }

    /// <summary>Throws when a tenant read isn't available while read-only, listing every one.</summary>
    public static void EnsureReadsAreAvailableWhenReadOnly(this WebApplication app, IReadOnlyList<ActionCatalogueEntry> actions)
    {
        ArgumentNullException.ThrowIfNull(app);

        var violations = Validate(actions);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException("Tenant reads must stay available while read-only:\n" + string.Join("\n", violations));
        }
    }
}
