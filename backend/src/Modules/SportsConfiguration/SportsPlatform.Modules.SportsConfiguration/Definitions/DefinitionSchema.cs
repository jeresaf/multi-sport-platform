using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Core;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// The first half of checking a configuration file (S05 D3): read the YAML, then check its shape against the file kind's
/// JSON Schema. Every problem names the file and the JSON path. The rules in code run only on a file that passes.
/// </summary>
internal sealed class DefinitionSchema(Func<string> schemaText)
{
    private readonly Lazy<JsonSchema> _schema = new(() => JsonSchema.FromText(schemaText()));

    /// <summary>
    /// The file's JSON tree when it is YAML of the right shape; otherwise null, with the problems.
    /// </summary>
    public JsonObject? Read(string file, string yaml, out IReadOnlyList<DefinitionProblem> problems)
    {
        JsonNode? root;
        try
        {
            root = YamlJson.Parse(yaml);
        }
        catch (YamlException exception)
        {
            problems = [new(file, "$", $"not valid YAML (line {exception.Start.Line}): {exception.Message}")];
            return null;
        }
        catch (InvalidOperationException)
        {
            // YamlDotNet's scanner throws this instead of a YamlException on some broken input (a flow sequence left
            // open before a mapping key), with no position.
            problems = [new(file, "$", "not valid YAML: the scanner gave up (an unclosed bracket or quote?)")];
            return null;
        }

        problems = ShapeProblems(file, root);
        return problems.Count > 0 ? null : root!.AsObject();
    }

    private List<DefinitionProblem> ShapeProblems(string file, JsonNode? root)
    {
        var results = _schema.Value.Evaluate(
            JsonSerializer.SerializeToElement(root),
            new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        return [.. Failures(results)
            .SelectMany(failure => failure.Errors!.Select(error => new DefinitionProblem(file, JsonPath(failure.InstanceLocation.ToString()), Message(failure, error.Value))))
            .Distinct()];
    }

    // "additionalProperties: false" fails with "All values fail against the false schema"; say what it means, as for a
    // member a "then" forbids ("min": false). Constants come JSON-escaped (Expected ""HEAD_TO_HEAD"").
    private static string Message(EvaluationResults failure, string message)
    {
        var path = failure.EvaluationPath.ToString();
        if (path.EndsWith("/additionalProperties", StringComparison.Ordinal))
        {
            return "unknown member";
        }

        return path.Contains("/then/properties/", StringComparison.Ordinal) && message.Contains("false schema", StringComparison.Ordinal)
            ? "not allowed for this type"
            : message.Replace("\\u0022", string.Empty, StringComparison.Ordinal);
    }

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
