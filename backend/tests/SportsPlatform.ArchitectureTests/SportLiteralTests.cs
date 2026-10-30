using System.Text.RegularExpressions;

namespace SportsPlatform.ArchitectureTests;

// AR-11 and S05 D6: generic code reads sports as data. Sport and variant codes may appear as string literals only in
// Sports Configuration's definitions, engines and renderers (and in tests, which are outside backend/src).
public sealed partial class SportLiteralTests
{
    // Only Sports Configuration's own folders; a generic module's Definitions folder is still scanned.
    private static readonly string[] _allowedFolders =
    [
        Path.Combine("src", "Modules", "SportsConfiguration", "SportsPlatform.Modules.SportsConfiguration", "Definitions"),
        Path.Combine("src", "Modules", "SportsConfiguration", "SportsPlatform.Modules.SportsConfiguration", "Engines"),
        Path.Combine("src", "Modules", "SportsConfiguration", "SportsPlatform.Modules.SportsConfiguration", "Renderers"),
    ];

    private static readonly string[] _scannedFolders = ["src", "tools"];

    private static readonly string _repositoryRoot = Path.GetDirectoryName(SolutionAssemblies.BackendDirectory)!;

    [Fact]
    public void Sport_and_variant_codes_are_read_from_the_reference_files()
    {
        var codes = SportCodes();

        codes.ShouldContain("FOOTBALL_ASSOCIATION");
        codes.ShouldContain("FOOTBALL");
        codes.ShouldContain("RUGBY_UNION_7");
        codes.ShouldContain("CRICKET_T20");
    }

    [Fact]
    public void Literals_finds_a_code_only_as_a_whole_string()
    {
        var found = Literals("var a = \"FOOTBALL\"; var b = \"FOOTBALL_PITCH\"; var c = \"football\";", new HashSet<string> { "FOOTBALL" });

        found.ShouldBe(["FOOTBALL"]);
    }

    [Fact]
    public void Generic_backend_code_has_no_sport_or_variant_literals()
    {
        var codes = SportCodes();
        var violations = _scannedFolders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(SolutionAssemblies.BackendDirectory, folder), "*.cs", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(SolutionAssemblies.BackendDirectory, path))
            .Where(path => !Segments(path).Any(segment => segment is "bin" or "obj")
                && !_allowedFolders.Any(folder => path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            .SelectMany(path => Literals(File.ReadAllText(Path.Combine(SolutionAssemblies.BackendDirectory, path)), codes).Select(code => $"{path}: \"{code}\""))
            .ToList();

        violations.ShouldBeEmpty();
    }

    // Variant and sport codes from every reference definition, plus the variants in README.md's table.
    private static HashSet<string> SportCodes()
    {
        var folder = Path.Combine(_repositoryRoot, "docs", "domain", "sports");
        var fromFiles = Directory.EnumerateFiles(folder, "*.yaml")
            .SelectMany(File.ReadLines)
            .Select(line => CodeLine().Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["code"].Value);
        var fromTable = File.ReadLines(Path.Combine(folder, "README.md"))
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal))
            .Select(line => TableVariant().Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["code"].Value);
        return [.. fromFiles, .. fromTable];
    }

    private static List<string> Literals(string source, IReadOnlySet<string> codes) =>
        [.. StringLiteral().Matches(source).Select(match => match.Groups["value"].Value).Where(codes.Contains)];

    private static string[] Segments(string path) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    [GeneratedRegex("^(variant|sport):\\s*(?<code>[A-Z][A-Z0-9_]*)\\s*$")]
    private static partial Regex CodeLine();

    [GeneratedRegex("^\\|[^|]+\\|\\s*`(?<code>[A-Z][A-Z0-9_]*)`")]
    private static partial Regex TableVariant();

    [GeneratedRegex("\"(?<value>[A-Z][A-Z0-9_]*)\"")]
    private static partial Regex StringLiteral();
}
