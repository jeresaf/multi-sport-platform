using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace SportsPlatform.BuildingBlocks.Results;

/// <summary>An expected failure with a stable code (<c>AREA_CONDITION</c>); the message may change, the code never.</summary>
[SuppressMessage("Naming", "CA1716", Justification = "'Error' is the spec's name; only a VB keyword, and the code base is C#.")]
public sealed partial record Error
{
    public Error(string code, string message, ErrorKind kind)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (!CodeFormat().IsMatch(code))
        {
            throw new ArgumentException($"Error code '{code}' must be '<AREA>_<CONDITION>' in upper snake case.", nameof(code));
        }

        Code = code;
        Message = message;
        Kind = kind;
    }

    public string Code { get; }

    public string Message { get; }

    public ErrorKind Kind { get; }

    [GeneratedRegex("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)+$")]
    private static partial Regex CodeFormat();
}
