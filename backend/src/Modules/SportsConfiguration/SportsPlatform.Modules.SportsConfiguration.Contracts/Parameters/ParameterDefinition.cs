namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

/// <summary>
/// A typed parameter with its default: <see cref="Min"/> and <see cref="Max"/> for a number, <see cref="Values"/> for a
/// choice (empty otherwise). The validator has checked that the default fits.
/// </summary>
public sealed record ParameterDefinition(
    ParameterType Type,
    string Label,
    ParameterValue Default,
    string? Description = null,
    int? Min = null,
    int? Max = null)
{
    public IReadOnlyList<string> Values { get; init; } = [];
}
