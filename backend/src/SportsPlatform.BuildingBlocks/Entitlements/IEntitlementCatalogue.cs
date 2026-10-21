namespace SportsPlatform.BuildingBlocks.Entitlements;

/// <summary>
/// Every entitlement declared in a module's <c>*Entitlements</c> class, for code that needs the definition behind an
/// action's entitlement code. It sits beside the action catalogue (AR-26, owner on #63 R3); the Host builds it at startup.
/// </summary>
public interface IEntitlementCatalogue
{
    /// <summary>In ordinal order of their codes.</summary>
    IReadOnlyList<EntitlementDefinition> All { get; }

    /// <summary>The entitlement with <paramref name="code"/>, or null when none is declared.</summary>
    EntitlementDefinition? Find(string code);
}
