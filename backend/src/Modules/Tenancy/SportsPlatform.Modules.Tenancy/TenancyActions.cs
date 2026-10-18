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
}
