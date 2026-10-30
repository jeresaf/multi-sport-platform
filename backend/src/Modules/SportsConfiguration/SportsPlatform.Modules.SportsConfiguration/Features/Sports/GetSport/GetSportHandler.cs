using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Sports.GetSport;

/// <summary>A variant's published definition at a version, the latest by default.</summary>
public sealed class GetSportHandler(ISportDefinitions definitions)
{
    public async Task<Result<SportDefinitionResponse>> HandleAsync(string variant, int? version, CancellationToken cancellationToken)
    {
        var definition = await definitions.GetAsync(variant, version, cancellationToken);
        return definition is null
            ? SportsConfigurationErrors.SportDefinitionNotFound
            : Result.Success(SportDefinitionResponse.From(definition));
    }
}
