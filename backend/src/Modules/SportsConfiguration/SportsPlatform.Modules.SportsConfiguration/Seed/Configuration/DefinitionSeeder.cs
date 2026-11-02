using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Domain.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Hashing;
using SportsPlatform.Modules.SportsConfiguration.Infrastructure;

namespace SportsPlatform.Modules.SportsConfiguration.Seed.Configuration;

/// <summary>
/// Seeds one kind of embedded configuration file (S05 D4), as the owner: a code and version not stored yet is inserted
/// with its canonical content and hash; one stored with the same hash is left alone; one stored with a different hash
/// fails the run with <c>&lt;KIND&gt;_IMMUTABLE</c> (a published version changes only through a new version), and two
/// files declaring one version fail it with <c>&lt;KIND&gt;_DUPLICATE_VERSION</c>. Never updates or deletes. A file the
/// validator refuses fails the run too.
/// </summary>
public abstract class DefinitionSeeder<TRow>(TimeProvider timeProvider) : ISeeder
    where TRow : class, IPublishedDefinition
{
    public abstract string Key { get; }

    public SeedCategory Category => SeedCategory.Configuration;

    public abstract int Order { get; }

    // The runner reads it before RunAsync: the files are validated here, so a bad one fails with the validator's problems.
    public string Checksum => ComputeChecksum(SeederVersion, Loaded.Select(d => (d.File, Hash: d.Row.ContentHash)));

    /// <summary>Bump in the kind's seeder when it writes something different from the same files.</summary>
    protected virtual int SeederVersion => 1;

    /// <summary>The kind's error prefix, e.g. <c>SPORT_DEFINITION</c>.</summary>
    protected abstract string ErrorPrefix { get; }

    /// <summary>The kind in a message, e.g. <c>Sport definitions</c>.</summary>
    protected abstract string Kind { get; }

    // Validated and resolved once per run (the runner uses one instance for the checksum and the run).
    private IReadOnlyList<(string File, TRow Row)> Loaded => _loaded ??= LoadAll();

    private IReadOnlyList<(string File, TRow Row)>? _loaded;

    public async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var db = context.CreateOwnerContext<SportsConfigurationDbContext>();
        var stored = (await db.Set<TRow>().AsNoTracking().ToListAsync(cancellationToken))
            .ToDictionary(d => (d.Code, d.Version), d => d.ContentHash);

        var inserted = 0;
        var declaredIn = new Dictionary<(string, int), string>();
        foreach (var (file, row) in Loaded)
        {
            var key = (row.Code, row.Version);
            if (!declaredIn.TryAdd(key, file))
            {
                throw new InvalidOperationException(
                    $"{ErrorPrefix}_DUPLICATE_VERSION: {file} and {declaredIn[key]} both declare {row.Code}@{row.Version}.");
            }

            if (stored.TryGetValue(key, out var storedHash))
            {
                if (!string.Equals(storedHash, row.ContentHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"{ErrorPrefix}_IMMUTABLE: {file}: {row.Code}@{row.Version} is published as {storedHash}, but the file " +
                        $"now hashes to {row.ContentHash}. A published definition never changes: give the change a new version.");
                }

                continue;
            }

            db.Add(row);
            stored.Add(key, row.ContentHash);
            inserted++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new SeedOutcome(inserted, 0, 0);
    }

    /// <summary>The kind's files, by name, with their text.</summary>
    protected abstract IEnumerable<(string File, string Yaml)> ReadFiles();

    protected abstract IReadOnlyList<DefinitionProblem> Validate(string file, string yaml);

    /// <summary>The row for a valid file's content; the typed model must read it, or every read of the row would fail later.</summary>
    protected abstract TRow ToRow(JsonNode content, string canonical, string hash, DateTimeOffset publishedAt);

    private List<(string File, TRow Row)> LoadAll()
    {
        var texts = ReadFiles().ToList();
        var problems = texts.SelectMany(text => Validate(text.File, text.Yaml)).ToList();
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"{Kind} are invalid:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        }

        var publishedAt = timeProvider.GetUtcNow();
        return [.. texts.Select(text =>
        {
            var content = YamlJson.Parse(text.Yaml)!;
            return (text.File, ToRow(content, CanonicalJson.Serialize(content), CanonicalJson.Hash(content), publishedAt));
        })];
    }

    private static string ComputeChecksum(int version, IEnumerable<(string File, string Hash)> definitions)
    {
        var data = new JsonObject
        {
            ["version"] = version,
            ["definitions"] = new JsonArray([.. definitions.Select(d => (JsonNode)new JsonObject { ["file"] = d.File, ["hash"] = d.Hash })]),
        };
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToJsonString())));
    }
}
