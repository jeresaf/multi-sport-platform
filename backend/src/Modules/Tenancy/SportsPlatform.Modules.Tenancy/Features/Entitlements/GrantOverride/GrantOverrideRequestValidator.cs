using FluentValidation;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Features.Entitlements.GrantOverride;

/// <summary>
/// A declared entitlement and a value of its type, else <c>TENANT_OVERRIDE_INVALID_VALUE</c> on the field; a reason of
/// 1–500 characters after trimming. Whether <c>effectiveUntil</c> has passed needs the tenant's date: the handler checks it.
/// </summary>
public sealed class GrantOverrideRequestValidator : AbstractValidator<GrantOverrideRequest>
{
    public GrantOverrideRequestValidator(IEntitlementCatalogue entitlements)
    {
        ArgumentNullException.ThrowIfNull(entitlements);
        RuleFor(request => request.EntitlementCode)
            .Must(code => code is not null && entitlements.Find(code) is not null)
            .WithMessage("'entitlementCode' must be a declared entitlement (GET /api/platform/v1/plans).")
            .WithErrorCode(TenancyErrors.OverrideInvalidValue.Code);
        RuleFor(request => request.Value)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("'value' must be an entitlement value: {\"enabled\": true}, {\"limit\": 5} or {\"level\": \"FULL\"}.")
            .Must((request, value) => request.EntitlementCode is not { } code || entitlements.Find(code) is not { } definition
                || definition.Accepts(value!))
            .WithMessage("'value' must be of the entitlement's type (enabled, limit or level) and, for a level, one of its levels.")
            .WithErrorCode(TenancyErrors.OverrideInvalidValue.Code);
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .WithErrorCode(CommonErrors.ReasonRequired.Code)
            .Must(reason => reason!.Trim().Length <= EntitlementOverride.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {EntitlementOverride.ReasonMaxLength} characters.");
    }
}
