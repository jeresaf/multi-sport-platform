using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Features.Members;

/// <summary>How a change to a membership touches the tenant's owners.</summary>
public enum OwnerImpact
{
    None,

    /// <summary>Changes an owner or the owner role without losing an active owner: only an owner may do it.</summary>
    ChangesOwner,

    /// <summary>Takes an active owner away (suspend, remove, revoke): only an owner may do it, and never the last one.</summary>
    RemovesOwner,
}

/// <summary>
/// The steps every member change shares, in order: load the request's tenant's membership with its roles
/// (<c>AUTH_MEMBERSHIP_NOT_FOUND</c>), check the version (<c>AUTH_CONCURRENCY_CONFLICT</c>), the owner rules (owner,
/// S03-T4: <c>AUTH_OWNER_REQUIRED</c>; D17: <c>AUTH_LAST_OWNER</c>), apply the domain change and save once with its
/// audit entry. A change that takes an owner away holds a per-tenant transaction lock while it checks the caller's
/// ownership, counts the other active owners and saves, so two such changes in one tenant can't each leave the other
/// as the last owner and both succeed.
/// </summary>
public sealed class MemberChanges(IdentityDbContext context, ICurrentActor actor, AuditScope audit)
{
    public async Task<Result<MemberResponse>> ApplyAsync(
        Guid membershipId,
        string? reason,
        uint? version,
        IfMatch ifMatch,
        Func<TenantMembership, OwnerImpact> impactOf,
        Func<TenantMembership, Result> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ifMatch);
        ArgumentNullException.ThrowIfNull(impactOf);
        ArgumentNullException.ThrowIfNull(change);
        audit.Describe(MemberEndpoints.EntityType, membershipId, reason);

        var membership = await context.Set<TenantMembership>()
            .Include(m => m.Roles)
            .SingleOrDefaultAsync(m => m.Id == membershipId, cancellationToken);
        if (membership is null)
        {
            return IdentityErrors.MembershipNotFound;
        }

        // A change saved by someone else after this read still conflicts on save: xmin is the concurrency token.
        var entry = context.Entry(membership);
        if (!RowVersion.IsSatisfied(entry.Property<uint>(AuditColumns.Version).CurrentValue, version, ifMatch))
        {
            return IdentityErrors.ConcurrencyConflict;
        }

        // A change that takes an owner away takes the tenant's owner lock first, so the caller's ownership and the
        // other owners are read after every owner change committed before it. Other owner changes (granting the role,
        // restoring an owner) check the caller without the lock: if the caller loses ownership a moment later, the
        // change was still made by an owner and leaves at least one.
        var impact = impactOf(membership);
        await using var transaction = impact == OwnerImpact.RemovesOwner
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        if (transaction is not null)
        {
            await LockOwnersAsync(membership.TenantId, cancellationToken);
        }

        if (impact != OwnerImpact.None && !await IsOwnerOrSystemAsync(cancellationToken))
        {
            return IdentityErrors.OwnerRequired;
        }

        if (transaction is not null && !await AnotherActiveOwnerAsync(membership, cancellationToken))
        {
            return IdentityErrors.LastOwner;
        }

        var email = await context.Set<User>().Where(u => u.Id == membership.UserId).Select(u => u.Email!).SingleAsync(cancellationToken);

        var changed = change(membership);
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        // Always update the membership row, so its version changes and a concurrent change to it conflicts: a role
        // change at the same instant as the last one would otherwise leave roles_changed_at, and the row, as they were.
        entry.Property(m => m.RolesChangedAt).IsModified = true;

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        if (transaction is not null)
        {
            // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
            await transaction.CommitAsync(CancellationToken.None);
        }

        return Result.Success(MemberResponse.From(membership, entry.Property<uint>(AuditColumns.Version).CurrentValue, email));
    }

    /// <summary>Whether the membership holds <c>TENANT_OWNER</c> tenant-wide (the only scope the role has).</summary>
    public static bool IsOwner(TenantMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        return membership.ActiveRoles.Any(IsOwnerRole);
    }

    public static bool IsOwnerRole(RoleAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        return assignment.IsSameAs(IdentityRoles.TenantOwner, AssignmentScope.Tenant, scopeId: null);
    }

    /// <summary>
    /// The caller is an active owner of the request's tenant. The system actor (seeds and commands, which the Host
    /// doesn't authorize) passes.
    /// </summary>
    private async Task<bool> IsOwnerOrSystemAsync(CancellationToken cancellationToken) =>
        actor.Kind == ActorKind.System
        || (actor.Kind == ActorKind.User && await context.Set<TenantMembership>().AnyAsync(
            m => m.UserId == actor.ActorId && m.Status == MembershipStatus.Active && m.Roles.Any(a =>
                a.RoleCode == IdentityRoles.TenantOwner && a.ScopeType == AssignmentScope.Tenant && a.RevokedAt == null),
            cancellationToken));

    /// <summary>
    /// The tenant's owner lock, held until the transaction ends. Key convention: <c>&lt;schema&gt;.&lt;purpose&gt;:&lt;id&gt;</c>,
    /// hashed to PostgreSQL's 64-bit advisory lock space.
    /// </summary>
    private async Task LockOwnersAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var key = $"identity.tenant_owners:{tenantId}";
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }

    /// <summary>
    /// Whether another active membership holds the owner role. Run under the owner lock: a new statement, so in READ
    /// COMMITTED it sees every owner change committed before the lock was taken.
    /// </summary>
    private Task<bool> AnotherActiveOwnerAsync(TenantMembership membership, CancellationToken cancellationToken) =>
        context.Set<TenantMembership>().AnyAsync(
            m => m.Id != membership.Id && m.Status == MembershipStatus.Active && m.Roles.Any(a =>
                a.RoleCode == IdentityRoles.TenantOwner && a.ScopeType == AssignmentScope.Tenant && a.RevokedAt == null),
            cancellationToken);
}
