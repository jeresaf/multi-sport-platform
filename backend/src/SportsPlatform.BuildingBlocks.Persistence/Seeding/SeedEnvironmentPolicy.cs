namespace SportsPlatform.BuildingBlocks.Persistence.Seeding;

/// <summary>
/// Where seeding may run (S01-T7): REFERENCE and CONFIGURATION everywhere; DEMO and TEST only in Development, Staging
/// and Testing (the automated tests); a reset only in Development.
/// </summary>
public static class SeedEnvironmentPolicy
{
    public const string Development = "Development";
    public const string Staging = "Staging";
    public const string Testing = "Testing";

    private static readonly string[] _sampleDataEnvironments = [Development, Staging, Testing];

    /// <summary>Why the run is refused in <paramref name="environment"/>, or null when it may run.</summary>
    public static string? Refusal(SeedRequest request, string environment)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Reset && !string.Equals(environment, Development, StringComparison.Ordinal))
        {
            return $"--reset is allowed only in Development (environment: {environment}).";
        }

        if (request.Category is SeedCategory.Demo or SeedCategory.Test
            && !_sampleDataEnvironments.Contains(environment, StringComparer.Ordinal))
        {
            return $"{request.Category.Code()} seeding is allowed only in {string.Join(", ", _sampleDataEnvironments)} " +
                $"(environment: {environment}).";
        }

        return null;
    }
}
