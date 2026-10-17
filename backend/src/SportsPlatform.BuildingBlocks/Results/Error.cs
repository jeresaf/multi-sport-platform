using System.Diagnostics.CodeAnalysis;

namespace SportsPlatform.BuildingBlocks.Results;

/// <summary>An expected failure with a stable code (<c>AREA_CONDITION</c>); the message may change, the code never.</summary>
[SuppressMessage("Naming", "CA1716", Justification = "'Error' is the spec's name; only a VB keyword, and the code base is C#.")]
public sealed record Error
{
    public Error(string code, string message, ErrorKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
        Kind = kind;
    }

    public string Code { get; }

    public string Message { get; }

    public ErrorKind Kind { get; }
}
