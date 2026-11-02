using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Formats.ListFormats;

/// <summary>Every published format at its latest version, by name, then code.</summary>
public sealed class ListFormatsHandler(IFormatDefinitions formats)
{
    public async Task<ListFormatsResponse> HandleAsync(CancellationToken cancellationToken) =>
        new([.. (await formats.ListAsync(cancellationToken)).Select(FormatDefinitionResponse.From)]);
}

public sealed record ListFormatsResponse(IReadOnlyList<FormatDefinitionResponse> Items);
