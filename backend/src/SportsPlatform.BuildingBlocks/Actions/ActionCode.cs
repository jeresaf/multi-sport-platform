using System.Text.RegularExpressions;

namespace SportsPlatform.BuildingBlocks.Actions;

/// <summary>Names a business operation: lowercase and dotted, e.g. <c>person.create</c>, <c>timeline.event.record</c>.</summary>
public sealed partial record ActionCode
{
    public ActionCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsValid(value))
        {
            throw new ArgumentException(
                $"Action code '{value}' must be lowercase '<resource>.<verb>' or '<resource>.<sub>.<verb>'.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    /// <summary>Whether <paramref name="value"/> has the format of an action code.</summary>
    public static bool IsValid(string value) => Format().IsMatch(value);

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*){1,2}$")]
    private static partial Regex Format();
}
