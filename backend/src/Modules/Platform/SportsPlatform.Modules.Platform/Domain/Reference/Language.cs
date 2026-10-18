using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Platform.Domain.Reference;

/// <summary>An ISO 639-1 language; <see cref="ReferenceEntry.Code"/> is the two-letter code (<c>sw</c>).</summary>
public sealed class Language : ReferenceEntry
{
    private string _name = string.Empty;

    public Language(string code, string name)
        : base(IdFor(code), code) => Apply(name);

    private Language()
    {
    }

    public string Name { get => _name; private set => _name = value; }

    public static Guid IdFor(string code) => DeterministicIds.Create($"platform.language:{code}");

    /// <summary>Takes the values from the reference data; returns whether anything changed.</summary>
    public bool Apply(string name) => Set(ref _name, name) | Reactivate();
}
