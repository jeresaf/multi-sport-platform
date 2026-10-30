using System.Globalization;
using Microsoft.AspNetCore.Mvc;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Sports.GetSport;

/// <summary>The definition's query parameter as sent (text, so every bad value is <c>VALIDATION_INVALID_QUERY</c>).</summary>
public sealed record GetSportRequest([FromQuery(Name = "version")] string? Version)
{
    public static bool TryParseVersion(string value, out int version) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out version) && version >= 1;

    /// <summary>The version asked for, or null for the latest; call it only after the validator accepted the request.</summary>
    public int? ToVersion() => Version switch
    {
        null => null,
        _ when TryParseVersion(Version, out var version) => version,
        _ => throw new InvalidOperationException("Validate the request first."),
    };
}
