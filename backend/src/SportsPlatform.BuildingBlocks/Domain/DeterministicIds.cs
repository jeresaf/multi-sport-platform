using System.Security.Cryptography;
using System.Text;

namespace SportsPlatform.BuildingBlocks.Domain;

/// <summary>
/// Name-based UUIDv5s (RFC 9562, section 5.5) for seeded records: the same stable key always gives the same ID, in
/// every environment and on every run. Keys look like <c>&lt;module&gt;.&lt;entity&gt;:&lt;natural key&gt;</c>, e.g.
/// <c>platform.country:UG</c>; never change a key once its records exist.
/// </summary>
public static class DeterministicIds
{
    /// <summary>The namespace of every MSP seed key. Fixed forever: changing it changes every seeded ID.</summary>
    public static readonly Guid SeedNamespace = new("3f6b8a52-1d7e-4c0b-9a4e-6b2d51c8e917");

    /// <summary>The ID for a seed key in <see cref="SeedNamespace"/>.</summary>
    public static Guid Create(string key) => Create(SeedNamespace, key);

    public static Guid Create(Guid namespaceId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[16 + nameBytes.Length];
        namespaceId.TryWriteBytes(input, bigEndian: true, out _);
        nameBytes.CopyTo(input, 16);

        // SHA-1 is what version 5 is defined with; it names records, it protects nothing.
#pragma warning disable CA5350
        var hash = SHA1.HashData(input);
#pragma warning restore CA5350

        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }
}
