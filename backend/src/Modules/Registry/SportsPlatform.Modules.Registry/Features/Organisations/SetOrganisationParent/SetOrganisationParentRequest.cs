using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Organisations;

namespace SportsPlatform.Modules.Registry.Features.Organisations.SetOrganisationParent;

/// <summary>
/// The organisation's new parent, or null for none, and why (S07 D5; audit <c>REQUIRED_WITH_REASON</c>). The reason is
/// kept permanently on the parent link and in the audit trail and is not redacted: it must not contain personal data.
/// <c>version</c>, when sent, must be the organisation's current one (or <c>If-Match</c>).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetOrganisationParentRequest(Guid? ParentId, string? Reason, uint? Version = null);

/// <summary>A reason of 1–500 characters after trimming (<c>VALIDATION_REASON_REQUIRED</c> otherwise); a parent ID, if any, not empty.</summary>
public sealed class SetOrganisationParentRequestValidator : AbstractValidator<SetOrganisationParentRequest>
{
    public SetOrganisationParentRequestValidator()
    {
        RuleFor(request => request.ParentId).NotEqual(Guid.Empty).WithMessage("'parentId' must be an organisation's ID or null.");
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .WithErrorCode(CommonErrors.ReasonRequired.Code)
            .Must(reason => reason!.Trim().Length <= OrganisationParentLink.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {OrganisationParentLink.ReasonMaxLength} characters.")
            .WithErrorCode(CommonErrors.ReasonRequired.Code);
    }
}
