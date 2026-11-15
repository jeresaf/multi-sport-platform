using FluentValidation;
using SportsPlatform.Modules.Tenancy.Domain.Sports;

namespace SportsPlatform.Modules.Tenancy.Features.Sports.DisableSport;

/// <summary>A reason of 1–500 characters after trimming.</summary>
public sealed class DisableSportRequestValidator : AbstractValidator<DisableSportRequest>
{
    public DisableSportRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .Must(reason => reason!.Trim().Length <= TenantSport.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {TenantSport.ReasonMaxLength} characters.");
    }
}
