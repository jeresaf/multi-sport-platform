using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
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

namespace SportsPlatform.Modules.Identity.Features.Invitations;

/// <summary>The audit metadata of an invitation: its roles and the pending invitations it replaced. Never the email.</summary>
public sealed record InvitationAuditMetadata(IReadOnlyList<string> RoleCodes, IReadOnlyList<Guid> Replaced);

/// <summary>
/// Invites a person to the request's tenant (S03 D12): a member's invitation (<c>membership.invite</c>) or a tenant's
/// owner from the platform console (<c>tenant.owner.invite</c>). An email whose user is already a member (not removed)
/// is <c>AUTH_ALREADY_MEMBER</c>; a pending invitation to the same email, expired or not, is revoked in the same save.
/// The invitation and <c>InvitationIssued</c> are saved with the audit entry; Identity's consumer makes the token and
/// sends the email (D13), so a rolled-back request sends nothing.
/// </summary>
public sealed class InviteHandler(
    IdentityDbContext context,
    RoleCatalogue roles,
    ILookupNormalizer normalizer,
    ICurrentActor actor,
    AuditScope audit,
    TimeProvider timeProvider)
{
    /// <summary>
    /// <c>membership.invite</c>: unknown roles and roles not held tenant-wide are <c>AUTH_ROLE_NOT_ASSIGNABLE</c>; only an
    /// owner (or the system actor, for seeds) invites with <c>TENANT_OWNER</c> (<c>AUTH_OWNER_REQUIRED</c>).
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

        return await InviteAsync(request.Email!, definitions, cancellationToken);
    }

    /// <summary><c>tenant.owner.invite</c>: an invitation with <c>TENANT_OWNER</c> to the route's tenant, by platform staff.</summary>
    public Task<Result<InvitationResponse>> InviteOwnerAsync(InviteOwnerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var owner = roles.Find(IdentityRoles.TenantOwner)
            ?? throw new InvalidOperationException($"The role catalogue has no {IdentityRoles.TenantOwner}.");
        return InviteAsync(request.Email!, [owner], cancellationToken);
    }

    private async Task<Result<InvitationResponse>> InviteAsync(
        string email, IReadOnlyCollection<RoleDefinition> definitions, CancellationToken cancellationToken)
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
        audit.Describe(InvitationEndpoints.EntityType, invitation.Id);

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
        foreach (var previous in replaced)
        {
            previous.Supersede(actor, now);
        }

        audit.Describe(metadata: new InvitationAuditMetadata(invitation.RoleCodes, [.. replaced.Select(i => i.Id)]));
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

        return Result.Success(InvitationResponse.From(invitation, entry.Property<uint>(AuditColumns.Version).CurrentValue, now));
    }
}
