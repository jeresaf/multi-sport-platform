using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.Modules.Identity;

/// <summary>The Identity module's actions; exported to docs/generated/actions.md by the CLI.</summary>
public static class IdentityActions
{
    // S03 D2: the caller's own account, under /api/account/v1.
    public static readonly ActionDefinition AccountLogin = new(
        "account.login",
        "Sign in with email and password; starts a session (cookie). Failures are audited with the reason, never the email.",
        ActionScope.Account,
        AuditRequirement.Required,
        since: "S03",
        allowsAnonymous: true);

    public static readonly ActionDefinition AccountLogout = new(
        "account.logout",
        "Sign out: ends this session.",
        ActionScope.Account,
        AuditRequirement.Required,
        since: "S03");

    public static readonly ActionDefinition AccountRead = new(
        "account.read",
        "Read the signed-in user: email, platform roles and tenants with their roles.",
        ActionScope.Account,
        AuditRequirement.None,
        since: "S03");

    public static readonly ActionDefinition PasswordForgot = new(
        "account.password.forgot",
        "Ask for a password reset email. Always accepted, whether or not the email has an account; the audit never records the email.",
        ActionScope.Account,
        AuditRequirement.Required,
        since: "S03",
        allowsAnonymous: true);

    public static readonly ActionDefinition PasswordReset = new(
        "account.password.reset",
        "Set a new password with the token from a reset email; signs out every session of the user and clears a lockout.",
        ActionScope.Account,
        AuditRequirement.Required,
        since: "S03",
        allowsAnonymous: true);

    public static readonly ActionDefinition PasswordChange = new(
        "account.password.change",
        "Change the signed-in user's password; signs out their other sessions.",
        ActionScope.Account,
        AuditRequirement.Required,
        since: "S03");

    public static readonly ActionDefinition PlatformAdminCreate = new(
        "platform.admin.create",
        "Create a platform administrator: a new user with PLATFORM_ADMIN (CLI create-platform-admin only; no endpoint).",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S03");

    // S03-T4b: the members of the request's tenant (X-Tenant-Id) and their roles.
    public static readonly ActionDefinition MembershipRead = new(
        "membership.read",
        "View the tenant's members: email, status and roles (every read is audited; the email is never in the audit).",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S03",
        scopableBy: [AssignmentScope.Tenant]);

    public static readonly ActionDefinition MembershipSuspend = new(
        "membership.suspend",
        "Suspend a member, with a reason: they lose access to the tenant and keep their roles until restored. Only an owner suspends an owner; never the last one.",
        ActionScope.Tenant,
        AuditRequirement.RequiredWithReason,
        since: "S03",
        scopableBy: [AssignmentScope.Tenant]);

    public static readonly ActionDefinition MembershipRestore = new(
        "membership.restore",
        "Restore a suspended member with the roles they held. Only an owner restores an owner.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S03",
        scopableBy: [AssignmentScope.Tenant]);

    public static readonly ActionDefinition MembershipRemove = new(
        "membership.remove",
        "Remove a member, with a reason (final; rejoining needs a new invitation). Only an owner removes an owner; never the last one.",
        ActionScope.Tenant,
        AuditRequirement.RequiredWithReason,
        since: "S03",
        scopableBy: [AssignmentScope.Tenant]);

    public static readonly ActionDefinition RoleAssign = new(
        "role.assign",
        "Give a member a role, tenant-wide (other scopes later). Only an owner grants TENANT_OWNER.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S03",
        scopableBy: [AssignmentScope.Tenant]);

    public static readonly ActionDefinition RoleRevoke = new(
        "role.revoke",
        "Revoke a member's role (the assignment is kept as history). Only an owner revokes TENANT_OWNER; never the last owner's.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S03",
        scopableBy: [AssignmentScope.Tenant]);

    // S03-T4c: invitations to the request's tenant (D12); the email goes out through the outbox (D13).
    public static readonly ActionDefinition MembershipInvite = new(
        "membership.invite",
        "Invite a person by email to join the tenant with tenant-wide roles; replaces a pending invitation to the same email. Only an owner invites an owner.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S03",
        scopableBy: [AssignmentScope.Tenant]);

    public static readonly ActionDefinition InvitationRevoke = new(
        "invitation.revoke",
        "Revoke a pending invitation: its link stops working.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S03",
        scopableBy: [AssignmentScope.Tenant]);

    public static readonly ActionDefinition TenantOwnerInvite = new(
        "tenant.owner.invite",
        "Invite a tenant's owner by email (platform console): an invitation with TENANT_OWNER, under the tenant named in the route.",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S03");
}
