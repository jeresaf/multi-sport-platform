using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Platform.Domain.Reference;

/// <summary>An ISO 3166-1 country; <see cref="ReferenceEntry.Code"/> is the alpha-2 code (<c>UG</c>).</summary>
public sealed class Country : ReferenceEntry
{
    private string _alpha3Code = string.Empty;
    private string _numericCode = string.Empty;
    private string _name = string.Empty;

    public Country(string code, string alpha3Code, string numericCode, string name)
        : base(IdFor(code), code) => Apply(alpha3Code, numericCode, name);

    private Country()
    {
    }

    public string Alpha3Code { get => _alpha3Code; private set => _alpha3Code = value; }

    public string NumericCode { get => _numericCode; private set => _numericCode = value; }

    public string Name { get => _name; private set => _name = value; }

    public static Guid IdFor(string code) => DeterministicIds.Create($"platform.country:{code}");

    /// <summary>Takes the values from the reference data; returns whether anything changed.</summary>
    public bool Apply(string alpha3Code, string numericCode, string name) =>
        Set(ref _alpha3Code, alpha3Code) | Set(ref _numericCode, numericCode) | Set(ref _name, name) | Reactivate();
}
