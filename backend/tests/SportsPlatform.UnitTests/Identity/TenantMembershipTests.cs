using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Identity;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Domain.Roles;

namespace SportsPlatform.UnitTests.Identity;

/// <summary>The membership state machine (state-machines.md, "Tenant membership") and its role assignments.</summary>
public sealed class TenantMembershipTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid _user = new("0192a1b2-0000-7000-8000-00000000c001");
    private static readonly Guid _competition = new("0192a1b2-0000-7000-8000-00000000c002");

    private static RoleDefinition Role(string code) => IdentityRoles.All.Single(role => role.Code == code);

    [Fact]
    public void A_new_membership_is_active_without_roles()
    {
        var membership = TenantMembership.Join(_user, invitedBy: SystemActor.Id, _now);

        membership.ShouldSatisfyAllConditions(
            m => m.Id.ShouldNotBe(Guid.Empty),
            m => m.UserId.ShouldBe(_user),
            m => m.Status.ShouldBe(MembershipStatus.Active),
            m => m.InvitedBy.ShouldBe(SystemActor.Id),
            m => m.JoinedAt.ShouldBe(_now),
            m => m.Roles.ShouldBeEmpty());
    }

    [Fact]
    public void A_membership_belongs_to_a_user() =>
        Should.Throw<ArgumentException>(() => TenantMembership.Join(Guid.Empty, invitedBy: null, _now));

    [Fact]
    public void Suspend_restore_and_remove_follow_the_state_machine()
    {
        var membership = TenantMembership.Join(_user, invitedBy: null, _now);

        membership.Restore(SystemActor.Instance, _now).Error.ShouldBe(IdentityErrors.MembershipTransitionInvalid);
        membership.Suspend("  Left the association  ", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        membership.ShouldSatisfyAllConditions(
            m => m.Status.ShouldBe(MembershipStatus.Suspended),
            m => m.StatusReason.ShouldBe("Left the association"),
            m => m.StatusChangedBy.ShouldBe(SystemActor.Id),
            m => m.StatusChangedAt.ShouldBe(_now));
        membership.Suspend("Again", SystemActor.Instance, _now).Error.ShouldBe(IdentityErrors.MembershipTransitionInvalid);

        membership.Restore(SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        membership.ShouldSatisfyAllConditions(m => m.Status.ShouldBe(MembershipStatus.Active), m => m.StatusReason.ShouldBeNull());

        membership.Remove("Duplicate account", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        membership.Status.ShouldBe(MembershipStatus.Removed);
        membership.Restore(SystemActor.Instance, _now).Error.ShouldBe(IdentityErrors.MembershipTransitionInvalid);
        membership.Suspend("Too late", SystemActor.Instance, _now).Error.ShouldBe(IdentityErrors.MembershipTransitionInvalid);
        membership.Remove("Twice", SystemActor.Instance, _now).Error.ShouldBe(IdentityErrors.MembershipTransitionInvalid);
    }

    [Fact]
    public void A_suspended_membership_can_be_removed()
    {
        var membership = TenantMembership.Join(_user, invitedBy: null, _now);
        membership.Suspend("Under review", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        membership.Remove("Review closed", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Suspending_and_removing_need_a_reason(string reason)
    {
        var membership = TenantMembership.Join(_user, invitedBy: null, _now);

        Should.Throw<ArgumentException>(() => membership.Suspend(reason, SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => membership.Remove(reason, SystemActor.Instance, _now));
    }

    [Fact]
    public void A_role_is_assigned_once_per_scope_and_revoked_by_id()
    {
        var membership = TenantMembership.Join(_user, invitedBy: null, _now);
        var manager = Role(IdentityRoles.CompetitionManager);

        membership.RolesChangedAt.ShouldBeNull();
        var tenantWide = membership.AssignRole(manager, AssignmentScope.Tenant, scopeId: null, SystemActor.Instance, _now).Value;
        membership.RolesChangedAt.ShouldBe(_now);
        membership.AssignRole(manager, AssignmentScope.Tenant, scopeId: null, SystemActor.Instance, _now)
            .Error.ShouldBe(IdentityErrors.RoleAlreadyAssigned);
        membership.AssignRole(manager, AssignmentScope.Competition, _competition, SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        tenantWide.ShouldSatisfyAllConditions(
            a => a.MembershipId.ShouldBe(membership.Id),
            a => a.RoleCode.ShouldBe(IdentityRoles.CompetitionManager),
            a => a.ScopeType.ShouldBe(AssignmentScope.Tenant),
            a => a.ScopeId.ShouldBeNull(),
            a => a.GrantedBy.ShouldBe(SystemActor.Id),
            a => a.IsActive.ShouldBeTrue());

        var later = _now.AddMinutes(5);
        membership.RevokeRole(tenantWide.Id, SystemActor.Instance, later).IsSuccess.ShouldBeTrue();
        membership.RolesChangedAt.ShouldBe(later);
        tenantWide.ShouldSatisfyAllConditions(a => a.IsActive.ShouldBeFalse(), a => a.RevokedAt.ShouldBe(later));
        membership.RevokeRole(tenantWide.Id, SystemActor.Instance, _now).Error.ShouldBe(IdentityErrors.RoleAssignmentNotFound);
        membership.ActiveRoles.ShouldHaveSingleItem().ScopeType.ShouldBe(AssignmentScope.Competition);

        // Revoked, it can be assigned again: the old row stays as history.
        membership.AssignRole(manager, AssignmentScope.Tenant, scopeId: null, SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        membership.Roles.Count.ShouldBe(3);
    }

    [Fact]
    public void A_role_is_assignable_only_where_authorization_md_allows_it()
    {
        var membership = TenantMembership.Join(_user, invitedBy: null, _now);

        // TEAM_MANAGER is team-only; VIEWER tenant-only; a platform role never; a resource scope needs its ID.
        membership.AssignRole(Role(IdentityRoles.TeamManager), AssignmentScope.Tenant, null, SystemActor.Instance, _now)
            .Error.ShouldBe(IdentityErrors.RoleNotAssignable);
        membership.AssignRole(Role(IdentityRoles.Viewer), AssignmentScope.Competition, _competition, SystemActor.Instance, _now)
            .Error.ShouldBe(IdentityErrors.RoleNotAssignable);
        membership.AssignRole(Role("PLATFORM_ADMIN"), AssignmentScope.Tenant, null, SystemActor.Instance, _now)
            .Error.ShouldBe(IdentityErrors.RoleNotAssignable);
        membership.AssignRole(Role(IdentityRoles.CompetitionManager), AssignmentScope.Competition, null, SystemActor.Instance, _now)
            .Error.ShouldBe(IdentityErrors.RoleNotAssignable);
        membership.AssignRole(Role(IdentityRoles.Viewer), AssignmentScope.Tenant, _competition, SystemActor.Instance, _now)
            .Error.ShouldBe(IdentityErrors.RoleNotAssignable);

        membership.Roles.ShouldBeEmpty();
    }

    [Fact]
    public void A_removed_membership_takes_no_role_changes()
    {
        var membership = TenantMembership.Join(_user, invitedBy: null, _now);
        var viewer = membership.AssignRole(Role(IdentityRoles.Viewer), AssignmentScope.Tenant, null, SystemActor.Instance, _now).Value;
        membership.Remove("Left the association", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        membership.AssignRole(Role(IdentityRoles.Registrar), AssignmentScope.Tenant, null, SystemActor.Instance, _now)
            .Error.ShouldBe(IdentityErrors.MembershipTransitionInvalid);
        membership.RevokeRole(viewer.Id, SystemActor.Instance, _now).Error.ShouldBe(IdentityErrors.MembershipTransitionInvalid);
    }
}
