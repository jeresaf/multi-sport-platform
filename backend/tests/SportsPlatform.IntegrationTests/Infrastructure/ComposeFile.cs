using System.Text.RegularExpressions;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>Test containers use docker compose's image tags, so tests and local development run the same versions.</summary>
public static class ComposeFile
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>The <c>image:</c> of the service whose image repository is <paramref name="repository"/>, with its tag.</summary>
    public static string Image(string repository)
    {
        var compose = File.ReadAllText(Path.Combine(RepositoryRoot, "docker-compose.yml"));
        var match = Regex.Match(compose, $@"^\s*image:\s*(?<image>{Regex.Escape(repository)}:\S+)\s*$", RegexOptions.Multiline);
        return match.Success
            ? match.Groups["image"].Value
            : throw new InvalidOperationException($"No {repository} image found in docker-compose.yml.");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("docker-compose.yml not found above the test output folder.");
    }
}
