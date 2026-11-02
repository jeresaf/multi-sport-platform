using System.Globalization;
using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

/// <summary>
/// The value of a typed parameter (a format's or a ruleset template's): a whole number, a flag, a choice or an ordered
/// list of codes. Its JSON is the bare value, <c>2</c>, <c>true</c>, <c>"RANDOM"</c> or <c>["POINTS", "WINS"]</c>
/// (<see cref="ParameterValueJsonConverter"/>). Two values are equal when their type and value are (a list's items in
/// order).
/// </summary>
[JsonConverter(typeof(ParameterValueJsonConverter))]
public sealed record ParameterValue
{
    private ParameterValue(ParameterType type, int number, bool flag, string? choice, IReadOnlyList<string> items)
    {
        Type = type;
        Number = number;
        Flag = flag;
        Choice = choice;
        Items = items;
    }

    public ParameterType Type { get; }

    /// <summary>A number's value; 0 for other types.</summary>
    public int Number { get; }

    /// <summary>A flag's value; false for other types.</summary>
    public bool Flag { get; }

    /// <summary>A choice's value; null for other types.</summary>
    public string? Choice { get; }

    /// <summary>A list's codes in order; empty for other types.</summary>
    public IReadOnlyList<string> Items { get; }

    public static ParameterValue Of(int number) => new(ParameterType.Number, number, false, null, []);

    public static ParameterValue Of(bool flag) => new(ParameterType.Flag, 0, flag, null, []);

    public static ParameterValue Of(string choice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(choice);
        return new(ParameterType.Choice, 0, false, choice, []);
    }

    /// <summary>A list of codes, in the order given; an item may not be blank (repeats are the parameter's to refuse).</summary>
    public static ParameterValue Of(IEnumerable<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        string[] list = [.. items];
        foreach (var item in list)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(item, nameof(items));
        }

        return new(ParameterType.List, 0, false, null, list);
    }

    public bool Equals(ParameterValue? other) =>
        other is not null
        && Type == other.Type
        && Number == other.Number
        && Flag == other.Flag
        && string.Equals(Choice, other.Choice, StringComparison.Ordinal)
        && Items.SequenceEqual(other.Items, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        hash.Add(Number);
        hash.Add(Flag);
        hash.Add(Choice, StringComparer.Ordinal);
        foreach (var item in Items)
        {
            hash.Add(item, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => Type switch
    {
        ParameterType.Number => Number.ToString(CultureInfo.InvariantCulture),
        ParameterType.Flag => Flag ? "true" : "false",
        ParameterType.List => $"[{string.Join(", ", Items)}]",
        _ => Choice!,
    };
}
