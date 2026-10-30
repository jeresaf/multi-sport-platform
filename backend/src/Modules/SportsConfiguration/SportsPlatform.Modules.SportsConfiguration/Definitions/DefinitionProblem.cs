namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// One problem in a configuration file: the file, the JSON path into it and what is wrong, e.g.
/// <c>football.yaml: $.event_types[3].actors[0].role: unknown actor role "SCORERR"</c>.
/// </summary>
public sealed record DefinitionProblem(string File, string Path, string Message)
{
    public override string ToString() => $"{File}: {Path}: {Message}";
}
