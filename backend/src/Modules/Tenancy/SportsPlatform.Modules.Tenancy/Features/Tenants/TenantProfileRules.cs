using System.Text.RegularExpressions;
using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants;

/// <summary>
/// Rules for the profile fields that create and update share. Codes are checked against Platform's active reference
/// data (<see cref="IReferenceData"/>) after their format. Messages name the field and what is accepted.
/// </summary>
internal static partial class TenantProfileRules
{
    public static IRuleBuilderOptions<T, string?> Name<T>(this IRuleBuilder<T, string?> rule, string field, int maxLength) => rule
        .Must(value => value?.Trim().Length is { } length && length >= 1 && length <= maxLength)
        .WithMessage($"'{field}' must be 1–{maxLength} characters.");

    public static IRuleBuilderOptions<T, string?> Country<T>(this IRuleBuilderInitial<T, string?> rule, IReferenceData reference) => rule
        .Cascade(CascadeMode.Stop)
        .Must(value => value is not null && CountryFormat().IsMatch(value))
        .WithMessage("'countryCode' must be an ISO 3166-1 alpha-2 code in upper case, such as UG.")
        .MustAsync((value, cancellationToken) => reference.IsActiveCountryAsync(value!, cancellationToken))
        .WithMessage("'countryCode' must be an active country (GET /api/public/v1/reference/countries).");

    public static IRuleBuilderOptions<T, string?> TimeZone<T>(this IRuleBuilderInitial<T, string?> rule, IReferenceData reference) => rule
        .Cascade(CascadeMode.Stop)
        .Must(value => value is { Length: >= 1 and <= 64 })
        .WithMessage("'timeZone' must be an IANA time zone ID, such as Africa/Kampala.")
        .MustAsync((value, cancellationToken) => reference.IsActiveTimeZoneAsync(value!, cancellationToken))
        .WithMessage("'timeZone' must be an active time zone (GET /api/public/v1/reference/time-zones).");

    public static IRuleBuilderOptions<T, string?> Currency<T>(this IRuleBuilderInitial<T, string?> rule, IReferenceData reference) => rule
        .Cascade(CascadeMode.Stop)
        .Must(value => value is not null && CurrencyFormat().IsMatch(value))
        .WithMessage("'currencyCode' must be an ISO 4217 code in upper case, such as UGX.")
        .MustAsync((value, cancellationToken) => reference.IsActiveCurrencyAsync(value!, cancellationToken))
        .WithMessage("'currencyCode' must be an active currency (GET /api/public/v1/reference/currencies).");

    /// <summary><c>ll</c> or <c>ll-CC</c>: an ISO 639-1 language, optionally with an ISO 3166-1 country.</summary>
    public static IRuleBuilderOptions<T, string?> Locale<T>(this IRuleBuilderInitial<T, string?> rule, IReferenceData reference) => rule
        .Cascade(CascadeMode.Stop)
        .Must(value => value is not null && LocaleFormat().IsMatch(value))
        .WithMessage("'defaultLocale' must be a language code with an optional country, such as en or en-UG.")
        .MustAsync(async (value, cancellationToken) =>
            await reference.IsActiveLanguageAsync(value![..2], cancellationToken)
            && (value.Length == 2 || await reference.IsActiveCountryAsync(value[3..], cancellationToken)))
        .WithMessage("'defaultLocale' must use an active language and, if given, an active country.");

    [GeneratedRegex("^[A-Z]{2}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CountryFormat();

    [GeneratedRegex("^[A-Z]{3}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyFormat();

    [GeneratedRegex("^[a-z]{2}(-[A-Z]{2})?\\z", RegexOptions.CultureInvariant)]
    private static partial Regex LocaleFormat();
}
