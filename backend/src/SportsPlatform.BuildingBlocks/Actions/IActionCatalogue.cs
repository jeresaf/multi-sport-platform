namespace SportsPlatform.BuildingBlocks.Actions;

/// <summary>
/// Every action declared in a module's <c>*Actions</c> class, for code that needs the definitions behind role codes
/// (capabilities, S03-T5) without referencing the declaring modules (AR-02). The Host builds it at startup.
/// </summary>
public interface IActionCatalogue
{
    /// <summary>In ordinal order of their codes.</summary>
    IReadOnlyList<ActionDefinition> All { get; }
}
