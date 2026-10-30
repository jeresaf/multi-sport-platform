using System.Text.RegularExpressions;

namespace SportsPlatform.ArchitectureTests;

// AR-11 and S05 D6: generic code reads sports as data. Sport and variant codes may appear as string literals only in
// Sports Configuration's engines and renderers (and in tests, which are outside backend/src).
public sealed partial class SportLiteralTests
{
    // Only Sports Configuration's engines and renderers. Definitions are YAML, never scanned; the C# next to them (the
    // validator, the seeders) is generic and is scanned like any other code.
    private static readonly string[] _allowedFolders =
    [
        Path.Combine("src", "Modules", "SportsConfiguration", "SportsPlatform.Modules.SportsConfiguration", "Engines"),
        Path.Combine("src", "Modules", "SportsConfiguration", "SportsPlatform.Modules.SportsConfiguration", "Renderers"),
    ];

    private static readonly string[] _scannedFolders = ["src", "tools"];

    private static readonly string _repositoryRoot = Path.GetDirectoryName(SolutionAssemblies.BackendDirectory)!;

    [Fact]
    public void Every_reference_file_gives_one_variant_and_one_sport_and_every_table_row_a_variant()
    {
        var files = Directory.EnumerateFiles(SportsFolder, "*.yaml").ToList();
        files.ShouldNotBeEmpty();
        foreach (var file in files)
        {
            var keys = CodeLines(file).Select(match => match.Groups["key"].Value).Order(StringComparer.Ordinal).ToList();
            keys.ShouldBe(["sport", "variant"], Path.GetFileName(file));
        }

        var rows = File.ReadLines(Path.Combine(SportsFolder, "README.md")).Where(line => line.StartsWith("| ", StringComparison.Ordinal) && line.Contains(".yaml", StringComparison.Ordinal)).ToList();
        rows.Count.ShouldBe(files.Count);
        rows.ShouldAllBe(row => TableVariant().IsMatch(row));
        SportCodes().ShouldContain("FOOTBALL_ASSOCIATION");
    }

    [Fact]
    public void A_code_line_may_end_with_a_comment()
    {
        CodeLine().Match("variant: FOOTBALL_ASSOCIATION   # never reused").Groups["code"].Value.ShouldBe("FOOTBALL_ASSOCIATION");
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

    private static string SportsFolder => Path.Combine(_repositoryRoot, "docs", "domain", "sports");

    private static IEnumerable<Match> CodeLines(string file) =>
        File.ReadLines(file).Select(line => CodeLine().Match(line)).Where(match => match.Success);

    // Variant and sport codes from every reference definition, plus the variants in README.md's table.
    private static HashSet<string> SportCodes()
    {
        var fromFiles = Directory.EnumerateFiles(SportsFolder, "*.yaml")
            .SelectMany(CodeLines)
            .Select(match => match.Groups["code"].Value);
        var fromTable = File.ReadLines(Path.Combine(SportsFolder, "README.md"))
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal))
            .Select(line => TableVariant().Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["code"].Value);
        return [.. fromFiles, .. fromTable];
    }

    private static List<string> Literals(string source, IReadOnlySet<string> codes) =>
        [.. StringLiteral().Matches(source).Select(match => match.Groups["value"].Value).Where(codes.Contains)];

    private static string[] Segments(string path) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    [GeneratedRegex("^(?<key>variant|sport):\\s*(?<code>[A-Z][A-Z0-9_]*)\\s*(#.*)?$")]
    private static partial Regex CodeLine();

    [GeneratedRegex("^\\|[^|]+\\|\\s*`(?<code>[A-Z][A-Z0-9_]*)`")]
    private static partial Regex TableVariant();

    [GeneratedRegex("\"(?<value>[A-Z][A-Z0-9_]*)\"")]
    private static partial Regex StringLiteral();
}
