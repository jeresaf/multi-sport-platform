using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Core;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// Checks a sport definition file: its shape against <c>sport-definition.schema.json</c>, then the references between
/// its lists (<see cref="SportDefinitionRules"/>). The seeder, the startup check and the tests all use it (S05 D3).
/// </summary>
public static class SportDefinitionValidator
{
    private static readonly Lazy<JsonSchema> _schema = new(() => JsonSchema.FromText(SportDefinitionFiles.Schema()));

    /// <summary>Every problem in the file; empty when it is valid.</summary>
    public static IReadOnlyList<DefinitionProblem> Validate(string file, string yaml)
    {
        JsonNode? root;
        try
        {
            root = YamlJson.Parse(yaml);
        }
        catch (YamlException exception)
        {
            return [new(file, "$", $"not valid YAML (line {exception.Start.Line}): {exception.Message}")];
        }

        var shape = ShapeProblems(file, root);
        return shape.Count > 0 ? shape : SportDefinitionRules.Check(file, root!.AsObject());
    }

    private static List<DefinitionProblem> ShapeProblems(string file, JsonNode? root)
    {
        var results = _schema.Value.Evaluate(
            JsonSerializer.SerializeToElement(root),
            new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        return [.. Failures(results)
            .SelectMany(failure => failure.Errors!.Select(error => new DefinitionProblem(file, JsonPath(failure.InstanceLocation.ToString()), Message(failure, error.Value))))
            .Distinct()];
    }

    // "additionalProperties: false" fails with "All values fail against the false schema"; say what it means. Constants
    // come JSON-escaped (Expected "\u0022HEAD_TO_HEAD\u0022").
    private static string Message(EvaluationResults failure, string message) =>
        failure.EvaluationPath.ToString().EndsWith("/additionalProperties", StringComparison.Ordinal)
            ? "unknown member"
            : message.Replace("\\u0022", string.Empty, StringComparison.Ordinal);

    // Only failing branches, and never an "if": its failure only selects the "then" or "else", which reports the
    // problem. A passing subschema can also carry errors of its own that are not problems with the file.
    private static IEnumerable<EvaluationResults> Failures(EvaluationResults results)
    {
        if (results.IsValid || results.EvaluationPath.ToString().EndsWith("/if", StringComparison.Ordinal))
        {
            yield break;
        }

        if (results.Errors is { Count: > 0 })
        {
            yield return results;
        }

        foreach (var failure in (results.Details ?? []).SelectMany(Failures))
        {
            yield return failure;
        }
    }

    /// <summary>A JSON Pointer as a JSON path: <c>/event_types/3/code</c> → <c>$.event_types[3].code</c>.</summary>
    internal static string JsonPath(string pointer) => "$" + string.Concat(pointer
        .Split('/', StringSplitOptions.RemoveEmptyEntries)
        .Select(segment => segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal))
        .Select(segment => segment.All(char.IsAsciiDigit) ? $"[{segment}]" : $".{segment}"));
}
