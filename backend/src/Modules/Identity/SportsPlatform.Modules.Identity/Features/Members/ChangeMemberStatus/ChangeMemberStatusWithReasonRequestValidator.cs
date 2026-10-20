using FluentValidation;
using SportsPlatform.Modules.Identity.Domain.Memberships;

namespace SportsPlatform.Modules.Identity.Features.Members.ChangeMemberStatus;

/// <summary>A reason of 1–500 characters after trimming.</summary>
public sealed class ChangeMemberStatusWithReasonRequestValidator : AbstractValidator<ChangeMemberStatusWithReasonRequest>
{
    public ChangeMemberStatusWithReasonRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .Must(reason => reason!.Trim().Length <= TenantMembership.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {TenantMembership.ReasonMaxLength} characters.");
    }
}
