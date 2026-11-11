using System.Text.Json;
using System.Text.Json.Nodes;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Hashing;
using SportsPlatform.Modules.SportsConfiguration.Parameters;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Rulesets;

/// <summary>
/// <see cref="IRulesetResolver"/> (S05 D11). In order: the template (a version that doesn't exist is
/// <c>RULESET_TEMPLATE_NOT_FOUND</c>) and the sport definition it pins, read from the stored definitions so a newer sport
/// version changes nothing; the format, one the template lists and that takes the sport's contest shape, and its
/// parameters; each override, a path the template lets a competition change with a value that fits it. Then the
/// overrides are merged into the template's values, the merged values are checked against the template schema and
/// against <see cref="RulesetValueChecks"/>, and the snapshot is written in canonical JSON and hashed. Each step answers
/// every problem it finds; a later step runs only when the earlier ones found none, since its checks read their values.
/// </summary>
public sealed class RulesetResolver(IRulesetTemplates templates, IFormatDefinitions formats, ISportDefinitions sports) : IRulesetResolver
{
    public const string TemplateField = "template";

    public const string TemplateVersionField = "template.version";

    public const string FormatCodeField = "format.code";

    public const string FormatVersionField = "format.version";

    private const string _formatParametersField = "format.parameters.";

    private const string _overridesField = "overrides.";

    private const string _valuesPath = "$.values.";

    public async Task<RulesetResolution> ResolveAsync(RulesetResolutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Format);
        ArgumentNullException.ThrowIfNull(request.Overrides);
        ArgumentNullException.ThrowIfNull(request.Format.Parameters);

        var template = await templates.GetAsync(request.TemplateCode, request.TemplateVersion, cancellationToken);
        if (template is null)
        {
            // As for formats: the version's field when the code exists and only that version doesn't.
            var problem = request.TemplateVersion is { } number && await templates.GetAsync(request.TemplateCode, null, cancellationToken) is not null
                ? new RulesetProblem(TemplateVersionField, SportsConfigurationErrors.RulesetTemplateNotFound, $"there is no version {number} of {request.TemplateCode}")
                : new RulesetProblem(TemplateField, SportsConfigurationErrors.RulesetTemplateNotFound, $"no template {request.TemplateCode}");
            return RulesetResolution.Failure([problem]);
        }

        // The seeders store a template only after the sport version it pins (S05 D4): a missing one is a bug, not input.
        var sport = await sports.GetAsync(template.Sport.Variant, template.Sport.Version, cancellationToken)
            ?? throw new InvalidOperationException($"Template {template.Code}@{template.Version} pins {template.Sport.Variant}@{template.Sport.Version}, which is not stored.");
        var tiebreakers = templates.Tiebreakers.ToDictionary(tiebreaker => tiebreaker.Code, StringComparer.Ordinal);

        List<RulesetProblem> problems = [];
        var format = await FormatAsync(template, sport, request.Format, problems, cancellationToken);
        if (format is not null)
        {
            problems.AddRange(ParameterChecks.FormatProblems(format, request.Format.Parameters)
                .Select(problem => new RulesetProblem(_formatParametersField + problem.Code, SportsConfigurationErrors.FormatParameterInvalid, problem.Message)));
        }

        foreach (var (path, value) in request.Overrides.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (OverrideProblem(template, sport, tiebreakers, path, value) is { } problem)
            {
                problems.Add(problem);
            }
        }

        if (problems.Count > 0 || format is null)
        {
            return RulesetResolution.Failure(problems);
        }

        var tree = Merge(template, request.Overrides);

        problems.AddRange(RulesetTemplateValidator.ShapeProblems(Skeleton(template, tree))
            .Select(problem => SchemaProblem(problem, request.Overrides)));
        if (problems.Count > 0)
        {
            return RulesetResolution.Failure(problems);
        }

        var values = RulesetTemplateContent.ReadValues(tree);
        problems.AddRange(RulesetValueChecks.Check(values, sport, tiebreakers.GetValueOrDefault)
            .SelectMany(problem => ValueProblems(problem, template, request.Overrides, found => RulesetValueChecks.Check(found, sport, tiebreakers.GetValueOrDefault))));
        if (problems.Count > 0)
        {
            return RulesetResolution.Failure(problems);
        }

        return RulesetResolution.Success(Snapshot(template, sport, format, ParameterChecks.WithFormatDefaults(format, request.Format.Parameters), values, tree));
    }

    private async Task<FormatDefinition?> FormatAsync(
        RulesetTemplate template, SportDefinition sport, RulesetFormatSelection selection, List<RulesetProblem> problems, CancellationToken cancellationToken)
    {
        if (!template.Formats.Contains(selection.Code, StringComparer.Ordinal))
        {
            problems.Add(new(FormatCodeField, SportsConfigurationErrors.RulesetIncompatibleFormat,
                $"{template.Code} works with {string.Join(", ", template.Formats)}, not {selection.Code}"));
            return null;
        }

        var field = selection.Version is null ? FormatCodeField : FormatVersionField;
        var format = await formats.GetAsync(selection.Code, selection.Version, cancellationToken);
        if (format is null)
        {
            problems.Add(new(field, SportsConfigurationErrors.RulesetIncompatibleFormat, selection.Version is { } version
                ? $"there is no version {version} of {selection.Code}"
                : $"{selection.Code} is not published"));
            return null;
        }

        // The template's own formats took the shape when it was published; another version of one may not.
        if (!format.CompatibleShapes.Contains(sport.Shape))
        {
            problems.Add(new(field, SportsConfigurationErrors.RulesetIncompatibleFormat,
                $"{format.Code}@{format.Version} doesn't take {RulesetValueChecks.Upper(sport.Shape)} contests"));
            return null;
        }

        return format;
    }

    // The first problem that stops the value (the resolution keeps one problem per field). Tiebreakers and tie resolutions the sport can't use answer
    // their own codes; anything else outside what the template allows is out of range.
    private static RulesetProblem? OverrideProblem(
        RulesetTemplate template, SportDefinition sport, Dictionary<string, TiebreakerDefinition> tiebreakers, string path, ParameterValue? value)
    {
        var field = _overridesField + path;
        if (!template.Editable.TryGetValue(path, out var parameter))
        {
            return new(field, SportsConfigurationErrors.RulesetParameterUnknown, $"{template.Code} has no parameter {path} a competition may change");
        }

        // Every bad code at once, so the list is fixed in one round.
        if (path == RulesetValueChecks.StandingsTiebreakersPath && value is { Type: ParameterType.List })
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            List<string> bad = [];
            foreach (var code in value.Items)
            {
                var problem = tiebreakers.GetValueOrDefault(code) switch
                {
                    null => $"unknown tiebreaker {code}",
                    { } tiebreaker when !tiebreaker.AppliesTo(sport.Sport) => $"tiebreaker {code} doesn't apply to {sport.Name}",
                    _ when !seen.Add(code) => $"tiebreaker {code} is listed twice",
                    _ => null,
                };
                if (problem is not null)
                {
                    bad.Add(problem);
                }
            }

            if (bad.Count > 0)
            {
                return new(field, SportsConfigurationErrors.RulesetUnknownTiebreaker, string.Join("; ", bad));
            }
        }

        if (path == RulesetValueChecks.TieResolutionPath && value is { Type: ParameterType.Choice, Choice: { } resolution }
            && !sport.Supports.TieResolutions.Contains(resolution, StringComparer.Ordinal))
        {
            return new(field, SportsConfigurationErrors.RulesetTieResolutionUnsupported, $"{sport.Name} doesn't support the tie resolution {resolution}");
        }

        return ParameterChecks.Problem(parameter, value) is { } outside
            ? new(field, SportsConfigurationErrors.RulesetParameterOutOfRange, $"{path} {outside}")
            : null;
    }

    // The schema checks a whole template: the merged values in a template that is otherwise the stored one's header.
    private static JsonObject Skeleton(RulesetTemplate template, JsonObject values) => new()
    {
        ["schema_version"] = template.SchemaVersion,
        ["code"] = template.Code,
        ["version"] = template.Version,
        ["name"] = template.Name,
        ["sport"] = new JsonObject { ["variant"] = template.Sport.Variant, ["version"] = template.Sport.Version },
        ["formats"] = new JsonArray([.. template.Formats.Select(code => (JsonNode)JsonValue.Create(code))]),
        ["editable"] = new JsonObject(),
        ["values"] = values.DeepClone(),
    };

    // A schema problem names a JSON path into the values ($.values.standings.points.win): the override at that path, or
    // the one holding it (a list item, $.values.standings.tiebreakers[2]).
    private static RulesetProblem SchemaProblem(DefinitionProblem problem, IReadOnlyDictionary<string, ParameterValue> overrides)
    {
        var path = problem.Path.StartsWith(_valuesPath, StringComparison.Ordinal) ? problem.Path[_valuesPath.Length..] : problem.Path;
        var owner = overrides.Keys
            .Where(key => path == key || path.StartsWith(key + ".", StringComparison.Ordinal) || path.StartsWith(key + "[", StringComparison.Ordinal))
            .OrderByDescending(key => key.Length)
            .FirstOrDefault() ?? path;
        return new(_overridesField + owner, SportsConfigurationErrors.RulesetParameterOutOfRange, $"{path}: {problem.Message}");
    }

    // A rule between values names its own path (roster.max) and may break because of another one (roster.min = 40 puts
    // roster.max's 30 below it). The template's own values break no rule, so the changes did: the problem goes to the
    // change it names, else to each change whose removal leaves fewer problems of its path and kind (two rules can break
    // at one path, each through another change), else (changes that only together, or each alone, break it) to every
    // change. Never to a path the request didn't send.
    private static IEnumerable<RulesetProblem> ValueProblems(
        RulesetValueProblem problem,
        RulesetTemplate template,
        IReadOnlyDictionary<string, ParameterValue> overrides,
        Func<RulesetValues, IReadOnlyList<RulesetValueProblem>> check)
    {
        var error = problem.Kind switch
        {
            RulesetValueProblemKind.TieResolution => SportsConfigurationErrors.RulesetTieResolutionUnsupported,
            RulesetValueProblemKind.Tiebreaker => SportsConfigurationErrors.RulesetUnknownTiebreaker,
            _ => SportsConfigurationErrors.RulesetParameterOutOfRange,
        };
        int Count(IEnumerable<KeyValuePair<string, ParameterValue>> applied) =>
            check(RulesetTemplateContent.ReadValues(Merge(template, applied))).Count(other => other.Path == problem.Path && other.Kind == problem.Kind);

        string[] causes;
        if (overrides.ContainsKey(problem.Path))
        {
            causes = [problem.Path];
        }
        else
        {
            var all = Count(overrides);
            causes = [.. overrides.Keys.Where(path => Count(overrides.Where(entry => entry.Key != path)) < all).Order(StringComparer.Ordinal)];
            if (causes.Length == 0)
            {
                causes = [.. overrides.Keys.Order(StringComparer.Ordinal)];
            }
        }

        foreach (var path in causes is [] ? [problem.Path] : causes)
        {
            yield return new(_overridesField + path, error, $"{problem.Path}: {problem.Message}");
        }
    }

    // The template's values as the file writes them, with the overrides set at their paths. Written from the typed model,
    // not the stored content: the round-trip test keeps the two equal, and a model change that would add a key (an
    // optional member) changes snapshot hashes, so it comes with a new schema version and new template versions.
    private static JsonObject Merge(RulesetTemplate template, IEnumerable<KeyValuePair<string, ParameterValue>> overrides)
    {
        var tree = RulesetTemplateContent.WriteValues(template.Values);
        foreach (var (path, value) in overrides)
        {
            Set(tree, path, JsonSerializer.SerializeToNode(value));
        }

        return tree;
    }

    private static RulesSnapshot Snapshot(
        RulesetTemplate template,
        SportDefinition sport,
        FormatDefinition format,
        IReadOnlyDictionary<string, ParameterValue> parameters,
        RulesetValues values,
        JsonObject tree)
    {
        var root = new JsonObject
        {
            ["sport"] = new JsonObject { ["variant"] = sport.Variant, ["version"] = sport.Version, ["hash"] = sport.ContentHash },
            ["format"] = new JsonObject
            {
                ["code"] = format.Code,
                ["version"] = format.Version,
                ["parameters"] = new JsonObject(parameters.Select(entry => KeyValuePair.Create(entry.Key, JsonSerializer.SerializeToNode(entry.Value)))),
            },
            ["template"] = new JsonObject { ["code"] = template.Code, ["version"] = template.Version },
        };
        foreach (var (section, node) in tree)
        {
            root[section] = node?.DeepClone();
        }

        return new RulesSnapshot(
            new RulesSnapshotSport(sport.Variant, sport.Version, sport.ContentHash),
            new RulesSnapshotFormat(format.Code, format.Version, parameters),
            new RulesSnapshotTemplate(template.Code, template.Version),
            values,
            CanonicalJson.Serialize(root),
            CanonicalJson.Hash(root));
    }

    // The template validator has checked that every editable path names a value, so each segment exists.
    private static void Set(JsonObject values, string path, JsonNode? value)
    {
        var segments = path.Split('.');
        var node = values;
        foreach (var segment in segments[..^1])
        {
            node = node[segment]!.AsObject();
        }

        node[segments[^1]] = value;
    }
}
