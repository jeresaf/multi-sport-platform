using FluentValidation;

namespace SportsPlatform.Modules.SportsConfiguration.Features;

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class VersionRequestValidator : AbstractValidator<VersionRequest>
{
    public VersionRequestValidator() =>
        RuleFor(request => request.Version)
            .Must(version => version is null || VersionRequest.TryParseVersion(version, out _))
            .WithMessage("'version' must be a whole number of at least 1.");
}
