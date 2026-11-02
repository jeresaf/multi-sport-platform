using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Formats;

/// <summary>
/// A published format as the API shows it (docs/domain/formats.md): camelCase names, UPPER_SNAKE values, with its
/// version's hash and publication time. Parameters stay a map keyed by their codes, as in the file (<c>cycles</c>).
/// </summary>
public sealed record FormatDefinitionResponse(
    string Code,
    int Version,
    string Name,
    string? Description,
    int SchemaVersion,
    string ContentHash,
    DateTimeOffset PublishedAt,
    IReadOnlyList<ContestShape> CompatibleShapes,
    FormatParticipantsResponse Participants,
    IReadOnlyList<FormatStageResponse> Stages,
    IReadOnlyDictionary<string, FormatParameterResponse> Parameters)
{
    public static FormatDefinitionResponse From(FormatDefinition format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return new FormatDefinitionResponse(
            format.Code,
            format.Version,
            format.Name,
            format.Description,
            format.SchemaVersion,
            format.ContentHash,
            format.PublishedAt,
            format.CompatibleShapes,
            new FormatParticipantsResponse(format.Participants.Min, format.Participants.Max),
            [.. format.Stages.Select(stage => new FormatStageResponse(stage.Code, stage.Type, stage.Generator))],
            format.Parameters.ToDictionary(parameter => parameter.Key, parameter => FormatParameterResponse.From(parameter.Value), StringComparer.Ordinal));
    }
}

/// <summary>How many participants the format takes, both ends included.</summary>
public sealed record FormatParticipantsResponse(int Min, int Max);

public sealed record FormatStageResponse(string Code, StageType Type, string Generator);

/// <summary>
/// A parameter: <c>min</c> and <c>max</c> for a number, <c>values</c> for a choice (empty otherwise); <c>default</c> is
/// the bare value (a whole number, true or false, or one of the values).
/// </summary>
public sealed record FormatParameterResponse(
    FormatParameterType Type,
    string Label,
    string? Description,
    int? Min,
    int? Max,
    IReadOnlyList<string> Values,
    ParameterValue Default)
{
    public static FormatParameterResponse From(ParameterDefinition parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        var type = parameter.Type switch
        {
            ParameterType.Number => FormatParameterType.Number,
            ParameterType.Flag => FormatParameterType.Flag,
            ParameterType.Choice => FormatParameterType.Choice,
            _ => throw new InvalidOperationException($"Unknown parameter type {parameter.Type}."),
        };
        return new FormatParameterResponse(type, parameter.Label, parameter.Description, parameter.Min, parameter.Max, parameter.Values, parameter.Default);
    }
}

/// <summary>A parameter's type in the API (<c>CHOICE</c>, <c>FLAG</c>, <c>NUMBER</c>); the files write <c>enum</c>, <c>bool</c>, <c>int</c>.</summary>
public enum FormatParameterType
{
    Choice,
    Flag,
    Number,
}
