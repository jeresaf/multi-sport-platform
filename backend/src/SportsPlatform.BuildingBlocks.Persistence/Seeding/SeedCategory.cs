namespace SportsPlatform.BuildingBlocks.Persistence.Seeding;

/// <summary>What a seeder writes, which decides where it may run and on which connection (data-conventions.md).</summary>
public enum SeedCategory
{
    /// <summary>Platform-global reference data (countries, currencies…); every environment; as <c>sp_migrator</c>.</summary>
    Reference,

    /// <summary>Platform configuration (sport definitions, formats…); every environment; as <c>sp_migrator</c>.</summary>
    Configuration,

    /// <summary>The fictional pilot customer's data, through application commands; never in Production.</summary>
    Demo,

    /// <summary>Data for automated tests, through application commands; never in Production.</summary>
    Test,
}

public static class SeedCategories
{
    /// <summary>
    /// The categories a run of <paramref name="category"/> seeds, in order: REFERENCE and CONFIGURATION come first,
    /// because everything else builds on them. DEMO and TEST never include each other.
    /// </summary>
    public static IReadOnlyList<SeedCategory> WithPrerequisites(this SeedCategory category) => category switch
    {
        SeedCategory.Reference => [SeedCategory.Reference],
        SeedCategory.Configuration => [SeedCategory.Reference, SeedCategory.Configuration],
        SeedCategory.Demo => [SeedCategory.Reference, SeedCategory.Configuration, SeedCategory.Demo],
        SeedCategory.Test => [SeedCategory.Reference, SeedCategory.Configuration, SeedCategory.Test],
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    /// <summary>Whether a seeder of this category writes read-only global tables, and so runs as the owner.</summary>
    public static bool RunsAsOwner(this SeedCategory category) =>
        category is SeedCategory.Reference or SeedCategory.Configuration;

    /// <summary>The UPPER_SNAKE name used by the CLI and stored in <c>seed_runs</c>.</summary>
    public static string Code(this SeedCategory category) => category.ToString().ToUpperInvariant();
}
