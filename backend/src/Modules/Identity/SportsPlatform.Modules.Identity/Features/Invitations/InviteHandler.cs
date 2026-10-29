using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Identity.Contracts.Invitations;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Features.Invitations.InviteMember;
using SportsPlatform.Modules.Identity.Features.Invitations.InviteOwner;
using SportsPlatform.Modules.Identity.Features.Members;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Configurations;
using SportsPlatform.Modules.Identity.Infrastructure.Users;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Identity.Features.Invitations;

/// <summary>The audit metadata of an invitation: its roles and the pending invitations it replaced. Never the email.</summary>
public sealed record InvitationAuditMetadata(IReadOnlyList<string> RoleCodes, IReadOnlyList<Guid> Replaced);

/// <summary>
/// Invites a person to the request's tenant (S03 D12): a member's invitation (<c>membership.invite</c>) or a tenant's
/// owner from the platform console (<c>tenant.owner.invite</c>). An email whose user is already a member (not removed)
/// is <c>AUTH_ALREADY_MEMBER</c>; a pending invitation to the same email, expired or not, is revoked in the same save.
/// The invitation and <c>InvitationIssued</c> are saved with the audit entry; Identity's consumer makes the token and
/// sends the email (D13), so a rolled-back request sends nothing. A member's invitation takes a staff user's seat
/// (S04 D13): it holds the tenant's staff-user lock while it counts and saves, so two invitations at the last free seat
/// can't both pass the quota.
/// </summary>
public sealed class InviteHandler(
    IdentityDbContext context,
    RoleCatalogue roles,
    ILookupNormalizer normalizer,
    ICurrentActor actor,
    AuditScope audit,
    ITenantContext tenantContext,
    ITenantDirectory tenants,
    StaffUsers staffUsers,
    IEntitlements entitlements,
    TimeProvider timeProvider)
{
    /// <summary>
    /// <c>membership.invite</c>: unknown roles and roles not held tenant-wide are <c>AUTH_ROLE_NOT_ASSIGNABLE</c>; only an
    /// owner (or the system actor, for seeds) invites with <c>TENANT_OWNER</c>, or replaces a pending invitation that
    /// carries it (<c>AUTH_OWNER_REQUIRED</c>); an invitation that would take the tenant past <c>MAX_STAFF_USERS</c> is
    /// <c>ENTITLEMENT_QUOTA_EXCEEDED</c> (replacing a pending invitation to the same email adds no one).
    /// </summary>
    public async Task<Result<InvitationResponse>> InviteMemberAsync(InviteMemberRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var definitions = new List<RoleDefinition>();
        foreach (var code in request.RoleCodes!)
        {
            if (roles.Find(code) is not { } role)
            {
                return IdentityErrors.RoleNotAssignable;
            }

            definitions.Add(role);
        }

        if (definitions.Any(role => role.Code == IdentityRoles.TenantOwner)
            && !await MemberChanges.IsOwnerOrSystemAsync(context, actor, cancellationToken))
        {
            return IdentityErrors.OwnerRequired;
        }

        return await InviteAsync(request.Email!, definitions, ownerRuleApplies: true, checksQuota: true, cancellationToken);
    }

    /// <summary>
    /// <c>tenant.owner.invite</c>: an invitation with <c>TENANT_OWNER</c> to the route's tenant, by platform staff; an
    /// archived tenant is <c>AUTH_TENANT_ARCHIVED</c>. It takes a seat but is never refused for the staff-user quota: staff
    /// can always give a tenant an owner (S04 D13).
    /// </summary>
    public async Task<Result<InvitationResponse>> InviteOwnerAsync(InviteOwnerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var owner = roles.Find(IdentityRoles.TenantOwner)
            ?? throw new InvalidOperationException($"The role catalogue has no {IdentityRoles.TenantOwner}.");

        // Tenant resolution found the route's tenant; it can't have gone since (tenants are never deleted).
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("The owner invitation runs under the route's tenant.");
        var tenant = await tenants.GetByIdAsync(tenantId, cancellationToken);
        if (tenant.IsFailure)
        {
            return tenant.Error;
        }

        if (tenant.Value.Status == TenantStatus.Archived)
        {
            return IdentityErrors.TenantArchived;
        }

        return await InviteAsync(request.Email!, [owner], ownerRuleApplies: false, checksQuota: false, cancellationToken);
    }

    private async Task<Result<InvitationResponse>> InviteAsync(
        string email,
        IReadOnlyCollection<RoleDefinition> definitions,
        bool ownerRuleApplies,
        bool checksQuota,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var trimmed = email.Trim();
        var normalized = normalizer.NormalizeEmail(trimmed);
        var created = Invitation.Create(trimmed, normalized, definitions, actor, now);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var invitation = created.Value;

        // The seat lock comes before every read, so the count below sees each invitation and membership committed before
        // it (READ COMMITTED: a new statement after the lock).
        await using var transaction = checksQuota ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
        if (transaction is not null)
        {
            await LockStaffUsersAsync(cancellationToken);
        }

        // Users are global (same module): an existing user is matched by the normalised email, as sign-in does.
        var userId = await context.Set<User>().Where(u => u.NormalizedEmail == normalized).Select(u => (Guid?)u.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (userId is { } id && await context.Set<TenantMembership>()
                .AnyAsync(m => m.UserId == id && m.Status != MembershipStatus.Removed, cancellationToken))
        {
            return IdentityErrors.AlreadyMember;
        }

        var replaced = await context.Set<Invitation>()
            .Where(i => i.NormalizedEmail == normalized && i.Status == InvitationStatus.Pending)
            .ToListAsync(cancellationToken);
        if (ownerRuleApplies
            && replaced.Any(InvitationRules.InvitesOwner)
            && !await MemberChanges.IsOwnerOrSystemAsync(context, actor, cancellationToken))
        {
            return IdentityErrors.OwnerRequired;
        }

        if (checksQuota)
        {
            var quota = await entitlements.CheckQuotaAsync(
                StaffUsers.Code, await staffUsers.CountAsync(normalized, cancellationToken), cancellationToken);
            if (quota.IsFailure)
            {
                return quota.Error;
            }
        }

        foreach (var previous in replaced)
        {
            previous.Supersede(actor, now);
        }

        // Only now is the invitation going to exist: refusals above are audited without an entity (#30 R2).
        audit.Describe(
            InvitationEndpoints.EntityType, invitation.Id,
            metadata: new InvitationAuditMetadata(invitation.RoleCodes, [.. replaced.Select(i => i.Id)]));
        var entry = context.Add(invitation);
        context.Publish(new InvitationIssued.V1(invitation.Id), InvitationEndpoints.EntityType, invitation.Id);

        try
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error;
            }
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: InvitationConfiguration.PendingEmailIndex,
        })
        {
            // Another invitation to the same email was saved between the read and this save.
            return IdentityErrors.ConcurrencyConflict;
        }

        if (transaction is not null)
        {
            // The audit counts as recorded once saved: a client hanging up now must not roll the invitation back unaudited.
            await transaction.CommitAsync(CancellationToken.None);
        }

        return Result.Success(InvitationResponse.From(invitation, entry.Property<uint>(AuditColumns.Version).CurrentValue, now));
    }

    /// <summary>
    /// The tenant's staff-user lock (<c>identity.staff_users:&lt;tenantId&gt;</c>, data-conventions.md), held until the
    /// transaction ends.
    /// </summary>
    private async Task LockStaffUsersAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("Invitations run under the request's tenant.");
        var key = $"identity.staff_users:{tenantId}";
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}
