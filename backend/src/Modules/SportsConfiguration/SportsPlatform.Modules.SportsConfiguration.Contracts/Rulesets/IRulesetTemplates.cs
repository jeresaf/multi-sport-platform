namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

/// <summary>
/// The published ruleset templates (S05 D11), read from the seeded rows and kept in memory until restart: configuration
/// never changes while the application runs. Also the tiebreaker catalogue the templates draw on.
/// </summary>
public interface IRulesetTemplates
{
    /// <summary>Every published template at its latest version, by name, then code.</summary>
    Task<IReadOnlyList<RulesetTemplate>> ListAsync(CancellationToken cancellationToken);

    /// <summary>A template at <paramref name="version"/> (the latest when null); null when there is none.</summary>
    Task<RulesetTemplate?> GetAsync(string code, int? version, CancellationToken cancellationToken);

    /// <summary>The tiebreaker catalogue, in the file's order.</summary>
    IReadOnlyList<TiebreakerDefinition> Tiebreakers { get; }
}
