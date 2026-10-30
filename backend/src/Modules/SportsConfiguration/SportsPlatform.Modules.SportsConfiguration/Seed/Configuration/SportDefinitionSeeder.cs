using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Domain.Sports;
using SportsPlatform.Modules.SportsConfiguration.Infrastructure;

namespace SportsPlatform.Modules.SportsConfiguration.Seed.Configuration;

/// <summary>
/// The embedded sport definitions (S05 D4), as the owner: a variant and version not stored yet is inserted with its
/// canonical content and hash; one stored with the same hash is left alone; one stored with a different hash fails the
/// run with <c>SPORT_DEFINITION_IMMUTABLE</c> (a published definition changes only through a new version). Never updates
/// or deletes. A file the validator refuses fails the run too.
/// </summary>
public sealed class SportDefinitionSeeder(SportDefinitionFiles files, TimeProvider timeProvider) : ISeeder
{
    public const string SeederKey = "config.configuration.sports";

    public const string ImmutableCode = "SPORT_DEFINITION_IMMUTABLE";

    /// <summary>Bump when the seeder writes something different from the same files.</summary>
    private const int _version = 1;

    public string Key => SeederKey;

    public SeedCategory Category => SeedCategory.Configuration;

    // Sports before formats and rulesets (D4); their seeders come with S05-T3 and T4.
    public int Order => 0;

    public string Checksum => ComputeChecksum(files.Names.Select(file => (file, Hash: CanonicalJson.Hash(YamlJson.Parse(files.Read(file))))));

    public async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var db = context.CreateOwnerContext<SportsConfigurationDbContext>();
        var stored = await db.Set<PublishedSportDefinition>()
            .ToDictionaryAsync(d => (d.Variant, d.Version), d => d.ContentHash, cancellationToken);

        var inserted = 0;
        var declaredIn = new Dictionary<(string, int), string>();
        foreach (var file in files.Names)
        {
            var definition = Load(file);
            var key = (definition.Variant, definition.Version);
            if (!declaredIn.TryAdd(key, file))
            {
                throw new InvalidOperationException(
                    $"{file} and {declaredIn[key]} both declare {definition.Variant}@{definition.Version}.");
            }

            if (stored.TryGetValue(key, out var storedHash))
            {
                if (!string.Equals(storedHash, definition.ContentHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"{ImmutableCode}: {file}: {definition.Variant}@{definition.Version} is published as {storedHash}, but the file " +
                        $"now hashes to {definition.ContentHash}. A published definition never changes: give the change a new version.");
                }

                continue;
            }

            db.Add(definition);
            stored.Add(key, definition.ContentHash);
            inserted++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new SeedOutcome(inserted, 0, 0);
    }

    private PublishedSportDefinition Load(string file)
    {
        var yaml = files.Read(file);
        var problems = SportDefinitionValidator.Validate(file, yaml);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"Sport definitions are invalid:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        }

        var content = YamlJson.Parse(yaml)!;
        // The typed model must read it, or every read of this row would fail later.
        var model = SportDefinitionContent.Read(content);
        return new PublishedSportDefinition(
            model.Variant,
            model.Sport,
            model.Version,
            model.Name,
            model.ScoringFamily,
            model.Shape,
            CanonicalJson.Serialize(content),
            CanonicalJson.Hash(content),
            timeProvider.GetUtcNow());
    }

    private static string ComputeChecksum(IEnumerable<(string File, string Hash)> definitions)
    {
        var data = new JsonObject
        {
            ["version"] = _version,
            ["definitions"] = new JsonArray([.. definitions.Select(d => (JsonNode)new JsonObject { ["file"] = d.File, ["hash"] = d.Hash })]),
        };
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToJsonString())));
    }
}
