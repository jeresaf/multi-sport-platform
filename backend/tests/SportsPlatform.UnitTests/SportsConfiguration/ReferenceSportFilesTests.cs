using SportsPlatform.Modules.SportsConfiguration.Definitions;

namespace SportsPlatform.UnitTests.SportsConfiguration;

// docs/domain/sports holds the reference files; the backend embeds its own copies (docs/domain/sports/README.md).
public sealed class ReferenceSportFilesTests
{
    private static readonly string _docs = Path.Combine(RepositoryRoot(), "docs", "domain", "sports");

    private static readonly SportDefinitionFiles _files = new();

    [Fact]
    public void Every_backend_definition_equals_its_docs_copy()
    {
        _files.Names.ShouldNotBeEmpty();
        foreach (var file in _files.Names)
        {
            _files.Read(file).ShouldBe(File.ReadAllText(Path.Combine(_docs, file)), $"{file} differs from docs/domain/sports/{file}: update both");
        }
    }

    // Not seeded until S15–S16, but the schema and the rules are written for every ADDITIVE reference file, so a rule
    // too narrow for them fails now rather than when their slice starts. Cricket (INNINGS) has no engine until S17.
    [Theory]
    [InlineData("rugby-union-15.yaml")]
    [InlineData("rugby-union-7.yaml")]
    [InlineData("basketball.yaml")]
    public void The_other_ADDITIVE_reference_files_are_valid(string file)
    {
        SportDefinitionValidator.Validate(file, File.ReadAllText(Path.Combine(_docs, file))).ShouldBeEmpty();
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend", "SportsPlatform.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("backend/SportsPlatform.slnx not found above the test output folder.");
    }
}
