using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

namespace SportsPlatform.Modules.SportsConfiguration.Parameters;

/// <summary>
/// Whether a value fits a typed parameter (a format's or a ruleset template's): its type, a number's bounds, a choice's
/// values, a list's values with each code once. The fixture generators and the ruleset resolver share these checks, so a
/// format parameter is refused the same way in a draw and in a preview.
/// </summary>
public static class ParameterChecks
{
    /// <summary>What is wrong with <paramref name="value"/>, after the parameter's name (<c>is from 1 to 5, not 6</c>); null when it fits.</summary>
    public static string? Problem(ParameterDefinition definition, ParameterValue? value)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (value is null)
        {
            return "has no value";
        }

        if (value.Type != definition.Type)
        {
            return $"is a {Describe(definition.Type)}, not {value}";
        }

        return definition.Type switch
        {
            ParameterType.Number when value.Number < definition.Min || value.Number > definition.Max =>
                $"is from {definition.Min} to {definition.Max}, not {value}",
            ParameterType.Choice when !definition.Values.Contains(value.Choice, StringComparer.Ordinal) =>
                $"is one of {string.Join(", ", definition.Values)}, not {value}",
            ParameterType.List => ListProblem(definition, value.Items),
            _ => null,
        };
    }

    /// <summary>
    /// Every problem with the format parameters given, in code order: an unknown code, or a value that doesn't fit.
    /// <c>Code</c> is the parameter's code; the message reads after it.
    /// </summary>
    public static IReadOnlyList<(string Code, string Message)> FormatProblems(FormatDefinition format, IReadOnlyDictionary<string, ParameterValue> given)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(given);
        List<(string, string)> problems = [];
        foreach (var (code, value) in given.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (!format.Parameters.TryGetValue(code, out var definition))
            {
                problems.Add((code, $"{format.Code} has no parameter {code}"));
            }
            else if (Problem(definition, value) is { } problem)
            {
                problems.Add((code, $"{code} {problem}"));
            }
        }

        return problems;
    }

    /// <summary>Every parameter of the format, in code order: the value given, else the default. The values must fit.</summary>
    public static IReadOnlyDictionary<string, ParameterValue> WithFormatDefaults(FormatDefinition format, IReadOnlyDictionary<string, ParameterValue> given)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(given);
        var resolved = new SortedDictionary<string, ParameterValue>(StringComparer.Ordinal);
        foreach (var (code, definition) in format.Parameters)
        {
            resolved[code] = given.TryGetValue(code, out var value) ? value : definition.Default;
        }

        return resolved;
    }

    /// <summary>The type's name in a message.</summary>
    public static string Describe(ParameterType type) => type switch
    {
        ParameterType.Number => "whole number",
        ParameterType.Flag => "flag",
        ParameterType.List => "list",
        _ => "choice",
    };

    private static string? ListProblem(ParameterDefinition definition, IReadOnlyList<string> items)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!definition.Values.Contains(item, StringComparer.Ordinal))
            {
                return $"takes only {string.Join(", ", definition.Values)}, not {item}";
            }

            if (!seen.Add(item))
            {
                return $"lists {item} twice";
            }
        }

        return null;
    }
}
