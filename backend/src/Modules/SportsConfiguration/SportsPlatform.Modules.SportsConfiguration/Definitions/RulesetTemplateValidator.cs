namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// Checks a ruleset template file: its shape against <c>ruleset-template.schema.json</c>, then its references, editable
/// parameters and values (<see cref="RulesetTemplateRules"/>). The seeder, the startup check and the tests all use it
/// (S05 D3, D11).
/// </summary>
public static class RulesetTemplateValidator
{
    private static readonly DefinitionSchema _schema = new(RulesetTemplateFiles.Schema);

    /// <summary>Every problem in the file; empty when it is valid.</summary>
    public static IReadOnlyList<DefinitionProblem> Validate(string file, string yaml, RulesetReferences references)
    {
        ArgumentNullException.ThrowIfNull(references);
        var root = _schema.Read(file, yaml, out var problems);
        return root is null ? problems : RulesetTemplateRules.Check(file, root, references);
    }
}
