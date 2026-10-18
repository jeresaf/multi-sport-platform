using Microsoft.AspNetCore.Mvc;

namespace SportsPlatform.Modules.Platform.Features.Reference.ListTimeZones;

/// <summary>The time zone list's query parameters as sent, checked by <see cref="ListTimeZonesRequestValidator"/>.</summary>
public sealed record ListTimeZonesRequest([FromQuery(Name = "country")] string? Country)
{
    /// <summary>An ISO 3166-1 alpha-2 code in upper case (<c>UG</c>).</summary>
    public static bool IsCountryCode(string value) => value is [>= 'A' and <= 'Z', >= 'A' and <= 'Z'];
}
