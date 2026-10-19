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

    public static readonly ActionDefinition PlatformAdminCreate = new(
        "platform.admin.create",
        "Create a platform administrator: a new user with PLATFORM_ADMIN (CLI create-platform-admin only; no endpoint).",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S03");
}
