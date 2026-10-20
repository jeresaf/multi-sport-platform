using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.Modules.Identity.Features.Invitations.LookupInvitation;

/// <summary>
/// A pending invitation as its invitee sees it: the tenant's display name, the invited email (the token proves the
/// invitee holds it), the tenant-wide roles, when it expires, and whether the email has an account: if it does, the
/// invitee signs in with it before accepting; if not, they choose a password when accepting.
/// </summary>
public sealed record LookupInvitationResponse(
    string TenantDisplayName,
    [property: PersonalData(PersonalDataClass.Private)] string Email,
    IReadOnlyList<string> RoleCodes,
    DateTimeOffset ExpiresAt,
    bool AccountExists);
