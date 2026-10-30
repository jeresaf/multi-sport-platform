using FluentValidation;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Sports.GetSport;

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class GetSportRequestValidator : AbstractValidator<GetSportRequest>
{
    public GetSportRequestValidator() =>
        RuleFor(request => request.Version)
            .Must(version => version is null || GetSportRequest.TryParseVersion(version, out _))
            .WithMessage("'version' must be a whole number of at least 1.");
}
