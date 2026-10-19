using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SportsPlatform.ArchitectureTests;

// S03 D16: no default credentials anywhere in code, seeds, tests or docs. Scans every file git tracks or would track
// for the obvious ones; the reviewer checks the rest. The local database roles' "*-dev-only" passwords are the
// documented exception (docker-compose.yml, appsettings.Development.json): they protect no user account.
public sealed partial class NoDefaultCredentialsTests
{
    // This file holds the patterns themselves.
    private static readonly string _self = Path.Combine("backend", "tests", "SportsPlatform.ArchitectureTests", "NoDefaultCredentialsTests.cs");

    private static readonly string[] _binaryExtensions = [".png", ".jpg", ".jpeg", ".gif", ".ico", ".webp", ".woff", ".woff2", ".pdf", ".zip"];

    [Fact]
    public void No_tracked_file_holds_a_well_known_default_password_or_a_platform_admin_password()
    {
        var root = Directory.GetParent(SolutionAssemblies.BackendDirectory)!.FullName;
        var findings = TrackedFiles(root)
            .Where(path => path != _self && !_binaryExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(path => (Path: path, FullPath: Path.Combine(root, path)))
            .Where(file => File.Exists(file.FullPath))
            .SelectMany(file => File.ReadLines(file.FullPath)
                .Select((line, index) => (Line: line, Number: index + 1))
                .Where(line => WellKnownPassword().IsMatch(line.Line) || AdminPasswordWithValue().IsMatch(line.Line))
                .Select(line => $"{file.Path}:{line.Number}"))
            .ToList();

        findings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("password: Passw0rd", true)]
    [InlineData("\"admin123\"", true)]
    [InlineData("MSP_ADMIN_PASSWORD=Kololo-hill-2026 dotnet run", true)]
    [InlineData("MSP_ADMIN_PASSWORD: 'nakasero-gate'", true)]
    [InlineData("export MSP_ADMIN_PASSWORD='<choose one>'", false)]
    [InlineData("MSP_ADMIN_PASSWORD=\"$PASSWORD\"", false)]
    [InlineData("reads the password from `MSP_ADMIN_PASSWORD` (refuses if it is missing)", false)]
    public void Patterns_find_the_obvious_cases_and_leave_placeholders(string line, bool found) =>
        (WellKnownPassword().IsMatch(line) || AdminPasswordWithValue().IsMatch(line)).ShouldBe(found);

    private static List<string> TrackedFiles(string root)
    {
        using var git = Process.Start(new ProcessStartInfo("git", "ls-files --cached --others --exclude-standard")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        })!;
        var files = git.StandardOutput.ReadToEnd()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => path.Replace('/', Path.DirectorySeparatorChar))
            .ToList();
        git.WaitForExit();
        git.ExitCode.ShouldBe(0, "the scan lists files with git");
        files.ShouldNotBeEmpty();
        return files;
    }

    [GeneratedRegex(@"\b(admin123|admin@123|password1|password123|passw0rd|p@ssw0rd|p@ssword|changeme|change_me|letmein|qwerty123|secret123|welcome123|default_?password)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WellKnownPassword();

    // A value right after the variable, unless it is a placeholder: <…>, $VAR or ${…}.
    [GeneratedRegex(@"MSP_ADMIN_PASSWORD\s*[=:]\s*[""']?(?![<$""'\s]|$)\S")]
    private static partial Regex AdminPasswordWithValue();
}
