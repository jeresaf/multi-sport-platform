using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Domain.Roles;

namespace SportsPlatform.Modules.Identity.Features.Invitations;

/// <summary>
/// The owner rule for invitations (owner, S03-T4): only an owner (or the system actor) invites with
/// <c>TENANT_OWNER</c>, and only an owner revokes or replaces an invitation that carries it, the platform's included.
/// </summary>
internal static class InvitationRules
{
    public static bool InvitesOwner(Invitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        return invitation.RoleCodes.Contains(IdentityRoles.TenantOwner, StringComparer.Ordinal);
    }
}
