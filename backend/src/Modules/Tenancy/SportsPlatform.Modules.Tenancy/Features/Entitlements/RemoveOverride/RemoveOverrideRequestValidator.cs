using FluentValidation;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Features.Entitlements.RemoveOverride;

/// <summary>A reason of 1–500 characters after trimming.</summary>
public sealed class RemoveOverrideRequestValidator : AbstractValidator<RemoveOverrideRequest>
{
    public RemoveOverrideRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .Must(reason => reason!.Trim().Length <= EntitlementOverride.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {EntitlementOverride.ReasonMaxLength} characters.");
    }
}
