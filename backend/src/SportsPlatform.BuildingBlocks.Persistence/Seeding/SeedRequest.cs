namespace SportsPlatform.BuildingBlocks.Persistence.Seeding;

/// <summary>
/// One seed run: a category (with its prerequisites, <see cref="SeedCategories.WithPrerequisites"/>) and, in
/// Development, a reset first (every module schema dropped and migrated again).
/// </summary>
public sealed record SeedRequest(SeedCategory Category, bool Reset = false)
{
    public const string Usage = "seed --category <REFERENCE|CONFIGURATION|DEMO|TEST> [--reset]";

    /// <summary>Parses the CLI's <c>seed</c> arguments; <paramref name="error"/> says what is wrong.</summary>
    public static bool TryParse(IReadOnlyList<string> args, out SeedRequest? request, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);
        request = null;
        error = null;

        SeedCategory? category = null;
        var reset = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--category" when category is null && i + 1 < args.Count:
                    if (!TryParseCategory(args[++i], out var parsed))
                    {
                        error = $"Unknown category '{args[i]}'. Usage: {Usage}";
                        return false;
                    }

                    category = parsed;
                    break;
                case "--reset" when !reset:
                    reset = true;
                    break;
                default:
                    error = $"Unexpected argument '{args[i]}'. Usage: {Usage}";
                    return false;
            }
        }

        if (category is null)
        {
            error = $"--category is required. Usage: {Usage}";
            return false;
        }

        request = new SeedRequest(category.Value, reset);
        return true;
    }

    // Only the exact codes: Enum.TryParse would also take numbers and mixed case.
    private static bool TryParseCategory(string value, out SeedCategory category)
    {
        foreach (var candidate in Enum.GetValues<SeedCategory>())
        {
            if (string.Equals(candidate.Code(), value, StringComparison.Ordinal))
            {
                category = candidate;
                return true;
            }
        }

        category = default;
        return false;
    }
}
