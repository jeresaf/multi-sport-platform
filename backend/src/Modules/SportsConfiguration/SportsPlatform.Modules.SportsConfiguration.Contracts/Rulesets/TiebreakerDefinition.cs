namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

/// <summary>
/// A tiebreaker of the catalogue (docs/domain/rulesets.md, "Tiebreaker catalogue"); Standings computes it (S13).
/// <see cref="Sports"/> names the sports it applies to (a definition's <c>sport</c>); empty means every sport.
/// </summary>
public sealed record TiebreakerDefinition(string Code, string Label, string Description)
{
    public IReadOnlyList<string> Sports { get; init; } = [];

    public bool AppliesTo(string sport) => Sports.Count == 0 || Sports.Contains(sport, StringComparer.Ordinal);
}
