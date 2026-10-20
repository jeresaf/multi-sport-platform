using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Features.Account;
using SportsPlatform.Modules.Identity.Features.Invitations.Acceptance;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Configurations;
using SportsPlatform.Modules.Identity.Infrastructure.Users;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Identity.Features.Invitations.AcceptInvitation;

/// <summary>The audit metadata of an acceptance: who joined, as which membership, with which roles. Never the email.</summary>
public sealed record InvitationAcceptedAuditMetadata(Guid UserId, Guid MembershipId, IReadOnlyList<string> RoleCodes, bool AccountCreated);

/// <summary>An accepted invitation's membership, or why it failed (a refused password names its rules).</summary>
public sealed record AcceptInvitationOutcome(AcceptInvitationResponse? Accepted, PasswordFailure? Failure);

/// <summary>
/// <c>invitation.accept</c> (S03 D12, T4c2): the invitee joins the invitation's tenant with its roles, tenant-wide,
/// granted by the inviter. If no user has the invited email, one is created with the password given and the email
/// confirmed (holding the token proves it); if one does, the caller must be signed in as that user. Every role code is
/// checked against the catalogue again, and a current member is refused. One save: the user, the membership and its
/// roles, the accepted invitation and the audit entry; a link used twice at once conflicts on the invitation's version
/// (or the current-membership and email indexes), so it gives one membership. Doesn't sign in.
/// </summary>
public sealed class AcceptInvitationHandler(
    InvitationByToken invitations,
    IdentityDbContext context,
    UserManager<User> users,
    RoleCatalogue roles,
    ITenantDirectory tenants,
    ICurrentActor actor,
    AuditScope audit,
    TimeProvider timeProvider)
{
    public async Task<AcceptInvitationOutcome> HandleAsync(AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        var found = await invitations.FindAsync(request.Token!, cancellationToken);
        if (found.IsFailure)
        {
            return Fail(found.Error);
        }

        var invitation = found.Value;
        var usable = invitation.CheckUsableAt(now);
        if (usable.IsFailure)
        {
            return Fail(usable.Error!);
        }

        var tenant = await tenants.GetByIdAsync(invitation.TenantId, cancellationToken);
        if (tenant.IsFailure)
        {
            return Fail(tenant.Error);
        }

        if (tenant.Value.Status == TenantStatus.Archived)
        {
            return Fail(IdentityErrors.TenantArchived);
        }

        // Roles are defined in code (D9): one the invitation names may have gone since it was made.
        var definitions = new List<RoleDefinition>();
        foreach (var code in invitation.RoleCodes)
        {
            if (roles.Find(code) is not { } role)
            {
                return Fail(IdentityErrors.RoleNotAssignable);
            }

            definitions.Add(role);
        }

        var user = await context.Set<User>().SingleOrDefaultAsync(u => u.NormalizedEmail == invitation.NormalizedEmail, cancellationToken);
        var accountCreated = user is null;
        if (user is null)
        {
            var created = await CreateUserAsync(invitation, request.Password);
            if (created.Failure is not null)
            {
                return new AcceptInvitationOutcome(null, created.Failure);
            }

            user = created.User!;
        }
        else
        {
            if (actor.Kind != ActorKind.User || actor.ActorId != user.Id)
            {
                return Fail(IdentityErrors.InvitationSignInRequired);
            }

            if (await context.Set<TenantMembership>().AnyAsync(m => m.UserId == user.Id && m.Status != MembershipStatus.Removed, cancellationToken))
            {
                return Fail(IdentityErrors.AlreadyMember);
            }
        }

        var membership = TenantMembership.Join(user.Id, invitation.InvitedBy, now);
        var inviter = new Inviter(invitation.InvitedBy);
        foreach (var role in definitions)
        {
            var assigned = membership.AssignRole(role, AssignmentScope.Tenant, scopeId: null, inviter, now);
            if (assigned.IsFailure)
            {
                return Fail(assigned.Error);
            }
        }

        var accepted = invitation.Accept(user.Id, membership.Id, now);
        if (accepted.IsFailure)
        {
            return Fail(accepted.Error!);
        }

        context.Add(membership);
        audit.Describe(metadata: new InvitationAcceptedAuditMetadata(user.Id, membership.Id, invitation.RoleCodes, accountCreated));

        try
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Fail(saved.Error!);
            }
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: TenantMembershipConfiguration.CurrentMembershipIndex
                or UserConfiguration.NormalizedEmailIndex or UserConfiguration.NormalizedUserNameIndex,
        })
        {
            // The same link (or another way in) won a race between the reads and this save.
            return Fail(IdentityErrors.ConcurrencyConflict);
        }

        return new AcceptInvitationOutcome(new AcceptInvitationResponse(invitation.TenantId, membership.Id), null);
    }

    /// <summary>A confirmed user with the invited email as written and the password, which must pass the policy.</summary>
    private async Task<(User? User, PasswordFailure? Failure)> CreateUserAsync(Invitation invitation, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return (null, IdentityErrors.PasswordRejected);
        }

        var user = User.Create(invitation.Email, emailConfirmed: true);
        // Validates the password, email and user name and hashes the password; the store doesn't save (UserStore).
        var created = await users.CreateAsync(user, password);
        if (created.Succeeded)
        {
            return (user, null);
        }

        if (created.Errors.All(PasswordFailure.IsPolicyError))
        {
            return (null, PasswordFailure.Rejected(created.Errors));
        }

        // Another acceptance created the user between the read above and this check.
        return created.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName))
            ? (null, IdentityErrors.ConcurrencyConflict)
            : throw new InvalidOperationException(
                $"The invited email was refused as a user's: {string.Join(", ", created.Errors.Select(e => e.Code))}.");
    }

    private static AcceptInvitationOutcome Fail(Error error) => new(null, error);

    /// <summary>The roles are granted by whoever chose them: the inviter (a user, or the system for seeds).</summary>
    private sealed record Inviter(Guid ActorId) : ICurrentActor
    {
        public ActorKind Kind => ActorId == SystemActor.Id ? ActorKind.System : ActorKind.User;
    }
}
