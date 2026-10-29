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
        scopableBy: [AssignmentScope.Tenant],
        availableWhenReadOnly: true);

    public static readonly ActionDefinition TenantSettingsUpdate = new(
        "tenant.settings.update",
        "Change the tenant's settings: time zone, locale, currency, public site, starting-soon window, minor consent policy.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S02",
        scopableBy: [AssignmentScope.Tenant]);

    public static readonly ActionDefinition TenantAccessRead = new(
        "tenant.access.read",
        "View the tenant's status, access mode and subscription, and why: available while the tenant is suspended, archived or read-only.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S03",
        scopableBy: [AssignmentScope.Tenant],
        availableWhenBlocked: true,
        availableWhenReadOnly: true);

    public static readonly ActionDefinition PlanRead = new(
        "plan.read",
        "View the plans and each entitlement's value on them.",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S04");

    public static readonly ActionDefinition SubscriptionRead = new(
        "subscription.read",
        "View a tenant's subscription periods, with their status today and the current period.",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S04");

    public static readonly ActionDefinition SubscriptionAssign = new(
        "subscription.assign",
        "Give a tenant a subscription period: a plan from one date to another, which must not overlap another period.",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S04");

    public static readonly ActionDefinition SubscriptionRenew = new(
        "subscription.renew",
        "Renew a tenant's subscription: a new period from the day after the latest one ends, optionally on another plan.",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S04");

    public static readonly ActionDefinition SubscriptionChangePlan = new(
        "subscription.change_plan",
        "Move a tenant to a higher plan now, until the current period ends; a lower plan waits for the renewal.",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S04");

    public static readonly ActionDefinition SubscriptionCancel = new(
        "subscription.cancel",
        "Cancel a tenant's current and scheduled subscription periods now, with a reason.",
        ActionScope.Platform,
        AuditRequirement.RequiredWithReason,
        since: "S04");

    public static readonly ActionDefinition EntitlementOverride = new(
        "entitlement.override",
        "Give a tenant its own value of an entitlement in place of its plan's, with a reason, optionally until a date; replaces an earlier override of it.",
        ActionScope.Platform,
        AuditRequirement.RequiredWithReason,
        since: "S04");

    public static readonly ActionDefinition EntitlementOverrideRemove = new(
        "entitlement.override.remove",
        "Remove a tenant's entitlement override, with a reason: its plan's value applies again.",
        ActionScope.Platform,
        AuditRequirement.RequiredWithReason,
        since: "S04");
}
