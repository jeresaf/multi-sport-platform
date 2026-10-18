using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Platform.Domain.Reference;

/// <summary>An ISO 4217 currency; <see cref="ReferenceEntry.Code"/> is the alphabetic code (<c>UGX</c>).</summary>
public sealed class Currency : ReferenceEntry
{
    private string _numericCode = string.Empty;
    private string _name = string.Empty;
    private int _minorUnits;

    public Currency(string code, string numericCode, string name, int minorUnits)
        : base(IdFor(code), code) => Apply(numericCode, name, minorUnits);

    private Currency()
    {
    }

    public string NumericCode { get => _numericCode; private set => _numericCode = value; }

    public string Name { get => _name; private set => _name = value; }

    /// <summary>Digits after the decimal point (UGX 0, KES 2).</summary>
    public int MinorUnits { get => _minorUnits; private set => _minorUnits = value; }

    public static Guid IdFor(string code) => DeterministicIds.Create($"platform.currency:{code}");

    /// <summary>Takes the values from the reference data; returns whether anything changed.</summary>
    public bool Apply(string numericCode, string name, int minorUnits) =>
        Set(ref _numericCode, numericCode) | Set(ref _name, name) | Set(ref _minorUnits, minorUnits) | Reactivate();
}
