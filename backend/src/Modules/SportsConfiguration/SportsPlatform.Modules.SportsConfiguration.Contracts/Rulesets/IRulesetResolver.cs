namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

/// <summary>
/// Resolves a ruleset template, a format and a competition's changes into an effective-rules snapshot (S05 D11,
/// docs/domain/rulesets.md): the template's values with the changes it allows, checked against the sport definition the
/// template pins, the format and the tiebreaker catalogue. Deterministic: the same inputs give the same snapshot and hash.
/// Nothing is stored; editions freeze the snapshot (S09).
/// </summary>
public interface IRulesetResolver
{
    /// <summary>The snapshot, or every problem with the field it names.</summary>
    Task<RulesetResolution> ResolveAsync(RulesetResolutionRequest request, CancellationToken cancellationToken);
}
