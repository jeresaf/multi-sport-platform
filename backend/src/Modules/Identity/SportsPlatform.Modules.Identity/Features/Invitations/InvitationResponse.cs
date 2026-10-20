using Microsoft.AspNetCore.Http;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Identity.Domain.Invitations;

namespace SportsPlatform.Modules.Identity.Features.Invitations;

/// <summary>
/// An invitation as the admin API shows it. <c>status</c> is <c>EXPIRED</c> for a pending one past <c>expiresAt</c>. The
/// email is shown to every holder of <c>membership.read</c>, like a member's (owner, S03-T4b), and never logged or
/// audited. <c>version</c> (also the <c>ETag</c>) changes whenever the invitation does, a resent email included.
/// </summary>
public sealed record InvitationResponse(
    Guid InvitationId,
    [property: PersonalData(PersonalDataClass.Private)] string Email,
    IReadOnlyList<string> RoleCodes,
    InvitationState Status,
    Guid InvitedBy,
    DateTimeOffset InvitedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? EmailSentAt,
    DateTimeOffset? RevokedAt,
    uint Version)
{
    public static InvitationResponse From(Invitation invitation, uint version, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        return new InvitationResponse(
            invitation.Id, invitation.Email, invitation.RoleCodes, invitation.StateAt(now), invitation.InvitedBy,
            invitation.InvitedAt, invitation.ExpiresAt, invitation.TokenIssuedAt, invitation.RevokedAt, version);
    }

    public void SetETag(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = RowVersion.EntityTag(Version).ToString();
    }
}
