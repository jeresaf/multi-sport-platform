using System.Text.Json;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Rulesets.PreviewRuleset;

/// <summary>
/// Resolves a template at a version (the latest by default) with the body's format and changes (S05 D16). Nothing is
/// stored. A value that isn't a whole number, true or false, a code or a list of codes is a problem on its own field,
/// beside the resolver's problems for the values that read.
/// </summary>
public sealed class PreviewRulesetHandler(IRulesetResolver resolver)
{
    private const string _expected = "must be a whole number, true or false, a code, or a list of codes";

    public async Task<RulesetResolution> HandleAsync(
        string code, int? version, PreviewRulesetBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var format = body.Format ?? throw new InvalidOperationException("Validate the body first.");

        List<RulesetProblem> problems = [];
        var parameters = Read(format.Parameters, "format.parameters.", SportsConfigurationErrors.FormatParameterInvalid, problems);
        var overrides = Read(body.Overrides, "overrides.", SportsConfigurationErrors.RulesetParameterOutOfRange, problems);

        var resolution = await resolver.ResolveAsync(
            new RulesetResolutionRequest(code, version, new RulesetFormatSelection(format.Code!, format.Version, parameters), overrides),
            cancellationToken);
        return problems.Count == 0 ? resolution : RulesetResolution.Failure([.. problems, .. resolution.Problems]);
    }

    private static Dictionary<string, ParameterValue> Read(
        IReadOnlyDictionary<string, JsonElement>? values, string fieldPrefix, Error error, List<RulesetProblem> problems)
    {
        var read = new Dictionary<string, ParameterValue>(StringComparer.Ordinal);
        foreach (var (key, element) in values ?? new Dictionary<string, JsonElement>())
        {
            if (TryRead(element) is { } value)
            {
                read[key] = value;
            }
            else
            {
                problems.Add(new(fieldPrefix + key, error, $"{key} {_expected}"));
            }
        }

        return read;
    }

    private static ParameterValue? TryRead(JsonElement element)
    {
        try
        {
            return element.Deserialize<ParameterValue>();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
