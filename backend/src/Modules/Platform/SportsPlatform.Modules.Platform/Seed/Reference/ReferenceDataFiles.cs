using System.Security.Cryptography;
using System.Text.Json;

namespace SportsPlatform.Modules.Platform.Seed.Reference;

/// <summary>
/// The reference data files embedded in this assembly (<c>Seed/Reference/Data</c>, written by
/// <c>backend/tools/reference-data/build-reference-data.cs</c>). Singleton; tests replace it to seed other data.
/// </summary>
public class ReferenceDataFiles
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        // A file missing a value fails the seed instead of writing an empty one.
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    private const string _resourcePrefix = "SportsPlatform.Modules.Platform.Seed.Reference.Data.";

    /// <summary>The bytes of a data file, e.g. <c>countries.json</c>.</summary>
    public virtual byte[] Read(string file)
    {
        using var stream = typeof(ReferenceDataFiles).Assembly.GetManifestResourceStream(_resourcePrefix + file)
            ?? throw new InvalidOperationException($"Reference data file {file} is not embedded.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    public IReadOnlyList<TItem> Items<TItem>(string file) =>
        JsonSerializer.Deserialize<ReferenceDataFile<TItem>>(Read(file), _options)?.Items
            ?? throw new InvalidOperationException($"Reference data file {file} is empty.");

    /// <summary>SHA-256 of the file and the seeder's version, for <c>ISeeder.Checksum</c>.</summary>
    public string Checksum(string file, int seederVersion) =>
        $"v{seederVersion}:{Convert.ToHexStringLower(SHA256.HashData(Read(file)))}";
}

/// <summary>The shape of every data file: where the data comes from, which release, and the entries.</summary>
public sealed record ReferenceDataFile<TItem>(string Source, string Version, IReadOnlyList<TItem> Items);

public sealed record CountryItem(string Code, string Alpha3Code, string NumericCode, string Name);

public sealed record CurrencyItem(string Code, string NumericCode, string Name, int MinorUnits);

public sealed record LanguageItem(string Code, string Name);

public sealed record TimeZoneItem(string Code, string? CountryCode);
