using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Identity.Features.Invitations;

/// <summary>
/// The scope's tenant's staff users for <c>MAX_STAFF_USERS</c> (S04 D13): memberships that aren't removed (active and
/// suspended), plus pending, unexpired invitations to emails that aren't members, so every invitation reserves a seat
/// until it is accepted, revoked or expires. Memberships and invitations are read under RLS.
/// </summary>
public sealed class StaffUsers(IdentityDbContext context, TimeProvider timeProvider) : IQuotaUsage
{
    public const string Code = "MAX_STAFF_USERS";

    public Task<int> CountAsync(CancellationToken cancellationToken) => CountAsync(exceptNormalizedEmail: null, cancellationToken);

    /// <summary>
    /// The count without a pending invitation to <paramref name="exceptNormalizedEmail"/>: a new invitation to that email
    /// replaces it, so it adds nothing.
    /// </summary>
    public async Task<int> CountAsync(string? exceptNormalizedEmail, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var members = context.Set<TenantMembership>().Where(m => m.Status != MembershipStatus.Removed);
        var memberEmails = members.Join(context.Set<User>(), m => m.UserId, u => u.Id, (_, u) => u.NormalizedEmail);
        var memberCount = await members.CountAsync(cancellationToken);
        var invitationCount = await context.Set<Invitation>().CountAsync(
            i => i.Status == InvitationStatus.Pending
                && i.ExpiresAt > now
                && i.NormalizedEmail != exceptNormalizedEmail
                && !memberEmails.Contains(i.NormalizedEmail),
            cancellationToken);
        return memberCount + invitationCount;
    }
}
