using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.Modules.Identity;

/// <summary>The Identity module's actions; exported to docs/generated/actions.md by the CLI.</summary>
public static class IdentityActions
{
    public static readonly ActionDefinition PlatformAdminCreate = new(
        "platform.admin.create",
        "Create a platform administrator: a new user with PLATFORM_ADMIN (CLI create-platform-admin only; no endpoint).",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S03");
}
