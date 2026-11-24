using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Teams;

namespace SportsPlatform.Modules.Registry.Features.Teams.ChangeTeamStatus;

/// <summary>
/// The status to move a team to and why (S07 D12; audit <c>REQUIRED_WITH_REASON</c>). The reason is kept permanently
/// in the status history and the audit trail and is not redacted: it must not contain personal data. <c>version</c>,
/// when sent, must be the team's current one (or <c>If-Match</c>).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeTeamStatusRequest(TeamStatus? Status, string? Reason, uint? Version = null);

/// <summary>A status, and a reason of 1–500 characters after trimming (<c>VALIDATION_REASON_REQUIRED</c> otherwise).</summary>
public sealed class ChangeTeamStatusRequestValidator : AbstractValidator<ChangeTeamStatusRequest>
{
    public ChangeTeamStatusRequestValidator()
    {
        RuleFor(request => request.Status).NotNull().WithMessage("'status' must not be empty.");
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .WithErrorCode(CommonErrors.ReasonRequired.Code)
            .Must(reason => reason!.Trim().Length <= TeamStatusChange.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {TeamStatusChange.ReasonMaxLength} characters.")
            .WithErrorCode(CommonErrors.ReasonRequired.Code);
    }
}
