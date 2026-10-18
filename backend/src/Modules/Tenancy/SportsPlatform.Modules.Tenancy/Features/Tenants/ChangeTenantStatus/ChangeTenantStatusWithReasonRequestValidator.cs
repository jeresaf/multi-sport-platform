using FluentValidation;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.ChangeTenantStatus;

/// <summary>A reason of 1–500 characters after trimming.</summary>
public sealed class ChangeTenantStatusWithReasonRequestValidator : AbstractValidator<ChangeTenantStatusWithReasonRequest>
{
    public ChangeTenantStatusWithReasonRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .Must(reason => reason!.Trim().Length <= TenantStatusChange.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {TenantStatusChange.ReasonMaxLength} characters.");
    }
}
