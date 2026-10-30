using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Sports.ListSports;

/// <summary>Every published variant at its latest version, by name, then variant code.</summary>
public sealed class ListSportsHandler(ISportDefinitions definitions)
{
    public async Task<ListSportsResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var variants = await definitions.ListAsync(cancellationToken);
        return new ListSportsResponse([.. variants.Select(variant => new SportVariantResponse(
            variant.Variant, variant.Sport, variant.Name, variant.ScoringFamily, variant.Shape, variant.LatestVersion))]);
    }
}

public sealed record ListSportsResponse(IReadOnlyList<SportVariantResponse> Items);

public sealed record SportVariantResponse(
    string Variant,
    string Sport,
    string Name,
    string ScoringFamily,
    ContestShape Shape,
    int LatestVersion);
