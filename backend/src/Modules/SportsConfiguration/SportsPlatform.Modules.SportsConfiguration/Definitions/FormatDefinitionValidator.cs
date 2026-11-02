namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// Checks a format definition file: its shape against <c>format-definition.schema.json</c>, then its generators and
/// parameters (<see cref="FormatDefinitionRules"/>). The seeder, the startup check and the tests all use it (S05 D3, D10).
/// </summary>
public static class FormatDefinitionValidator
{
    private static readonly DefinitionSchema _schema = new(FormatDefinitionFiles.Schema);

    /// <summary>Every problem in the file; empty when it is valid.</summary>
    public static IReadOnlyList<DefinitionProblem> Validate(string file, string yaml)
    {
        var root = _schema.Read(file, yaml, out var problems);
        return root is null ? problems : FormatDefinitionRules.Check(file, root);
    }
}
