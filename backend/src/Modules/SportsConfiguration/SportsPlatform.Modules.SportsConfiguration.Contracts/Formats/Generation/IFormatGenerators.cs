using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Formats.Generation;

/// <summary>
/// The fixture generators (S05 D10, docs/domain/formats.md): pure functions of a format, its parameters, the ordered
/// entrants and a seed. The same input always gives the same structure and <see cref="GeneratedFormat.OutputHash"/>, on
/// any machine and .NET version, so a stored input reproduces a draw.
/// </summary>
public interface IFormatGenerators
{
    /// <summary>
    /// The stages, rounds and pairings of <paramref name="format"/> for <paramref name="input"/>; or
    /// <c>FORMAT_PARTICIPANT_COUNT_INVALID</c>, <c>FORMAT_PARAMETER_INVALID</c> or <c>FORMAT_ENTRANTS_INVALID</c>.
    /// </summary>
    Result<GeneratedFormat> Generate(FormatDefinition format, FormatGenerationInput input);
}
