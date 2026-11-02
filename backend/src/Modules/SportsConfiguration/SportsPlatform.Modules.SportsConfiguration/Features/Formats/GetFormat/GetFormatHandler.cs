using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Formats.GetFormat;

/// <summary>A format at a version, the latest by default.</summary>
public sealed class GetFormatHandler(IFormatDefinitions formats)
{
    public async Task<Result<FormatDefinitionResponse>> HandleAsync(string code, int? version, CancellationToken cancellationToken)
    {
        var format = await formats.GetAsync(code, version, cancellationToken);
        return format is null
            ? SportsConfigurationErrors.FormatDefinitionNotFound
            : Result.Success(FormatDefinitionResponse.From(format));
    }
}
