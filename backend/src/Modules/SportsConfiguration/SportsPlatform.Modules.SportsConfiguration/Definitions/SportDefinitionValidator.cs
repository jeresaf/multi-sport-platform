namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// Checks a sport definition file: its shape against <c>sport-definition.schema.json</c>, then the references between
/// its lists (<see cref="SportDefinitionRules"/>). The seeder, the startup check and the tests all use it (S05 D3).
/// </summary>
public static class SportDefinitionValidator
{
    private static readonly DefinitionSchema _schema = new(SportDefinitionFiles.Schema);

    /// <summary>Every problem in the file; empty when it is valid.</summary>
    public static IReadOnlyList<DefinitionProblem> Validate(string file, string yaml)
    {
        var root = _schema.Read(file, yaml, out var problems);
        return root is null ? problems : SportDefinitionRules.Check(file, root);
    }
}
