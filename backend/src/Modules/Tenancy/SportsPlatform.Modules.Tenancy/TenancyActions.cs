using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.Modules.Tenancy;

/// <summary>The Tenancy module's actions; exported to docs/generated/actions.md by the CLI.</summary>
public static class TenancyActions
{
    public static readonly ActionDefinition TenantCreate = new(
        "tenant.create",
        "Create a tenant (status PROVISIONING).",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S02");

    public static readonly ActionDefinition TenantRead = new(
        "tenant.read",
        "View tenants and their profiles.",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S02");

    public static readonly ActionDefinition TenantUpdate = new(
        "tenant.update",
        "Change a tenant's profile: names, country, time zone, locale, currency, public site.",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S02");

    public static readonly ActionDefinition TenantActivate = new(
        "tenant.activate",
        "Activate a provisioned tenant (PROVISIONING → ACTIVE).",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S02");

    public static readonly ActionDefinition TenantSuspend = new(
        "tenant.suspend",
        "Suspend an active tenant, with a reason (ACTIVE → SUSPENDED).",
        ActionScope.Platform,
        AuditRequirement.RequiredWithReason,
        since: "S02");

    public static readonly ActionDefinition TenantRestore = new(
        "tenant.restore",
        "Restore a suspended tenant (SUSPENDED → ACTIVE).",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S02");

    public static readonly ActionDefinition TenantArchive = new(
        "tenant.archive",
        "Archive a tenant, with a reason (ACTIVE or SUSPENDED → ARCHIVED).",
        ActionScope.Platform,
        AuditRequirement.RequiredWithReason,
        since: "S02");

    public static readonly ActionDefinition TenantUnarchive = new(
        "tenant.unarchive",
        "Bring an archived tenant back, with a reason (ARCHIVED → SUSPENDED).",
        ActionScope.Platform,
        AuditRequirement.RequiredWithReason,
        since: "S02");

    public static readonly ActionDefinition TenantSettingsRead = new(
        "tenant.settings.read",
        "View the tenant's settings: time zone, locale, currency, public site, starting-soon window, minor consent policy.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S02",
        scopableBy: [AssignmentScope.Tenant]);

    public static readonly ActionDefinition TenantSettingsUpdate = new(
        "tenant.settings.update",
        "Change the tenant's settings: time zone, locale, currency, public site, starting-soon window, minor consent policy.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S02",
        scopableBy: [AssignmentScope.Tenant]);

    public static readonly ActionDefinition TenantAccessRead = new(
        "tenant.access.read",
        "View the tenant's status and access mode, and why: available while the tenant is suspended or archived.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S03",
        scopableBy: [AssignmentScope.Tenant],
        availableWhenBlocked: true);
}
