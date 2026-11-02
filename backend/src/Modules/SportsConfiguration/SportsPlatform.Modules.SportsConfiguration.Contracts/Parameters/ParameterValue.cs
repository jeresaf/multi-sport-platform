using System.Globalization;
using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

/// <summary>
/// The value of a typed parameter (a format's or a ruleset template's): a whole number, a flag or a choice. Its JSON is
/// the bare value, <c>2</c>, <c>true</c> or <c>"RANDOM"</c> (<see cref="ParameterValueJsonConverter"/>).
/// </summary>
[JsonConverter(typeof(ParameterValueJsonConverter))]
public sealed record ParameterValue
{
    private ParameterValue(ParameterType type, int number, bool flag, string? choice)
    {
        Type = type;
        Number = number;
        Flag = flag;
        Choice = choice;
    }

    public ParameterType Type { get; }

    /// <summary>A number's value; 0 for other types.</summary>
    public int Number { get; }

    /// <summary>A flag's value; false for other types.</summary>
    public bool Flag { get; }

    /// <summary>A choice's value; null for other types.</summary>
    public string? Choice { get; }

    public static ParameterValue Of(int number) => new(ParameterType.Number, number, false, null);

    public static ParameterValue Of(bool flag) => new(ParameterType.Flag, 0, flag, null);

    public static ParameterValue Of(string choice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(choice);
        return new(ParameterType.Choice, 0, false, choice);
    }

    public override string ToString() => Type switch
    {
        ParameterType.Number => Number.ToString(CultureInfo.InvariantCulture),
        ParameterType.Flag => Flag ? "true" : "false",
        _ => Choice!,
    };
}
