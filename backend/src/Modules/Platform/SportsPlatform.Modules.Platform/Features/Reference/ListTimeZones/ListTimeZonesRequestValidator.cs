using FluentValidation;

namespace SportsPlatform.Modules.Platform.Features.Reference.ListTimeZones;

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class ListTimeZonesRequestValidator : AbstractValidator<ListTimeZonesRequest>
{
    public ListTimeZonesRequestValidator() =>
        RuleFor(request => request.Country)
            .Must(country => country is null || ListTimeZonesRequest.IsCountryCode(country))
            .WithMessage("'country' must be an ISO 3166-1 alpha-2 code in upper case, such as UG.");
}
