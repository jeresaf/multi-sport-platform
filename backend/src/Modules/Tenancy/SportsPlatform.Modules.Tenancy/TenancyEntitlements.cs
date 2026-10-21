using SportsPlatform.BuildingBlocks.Entitlements;

namespace SportsPlatform.Modules.Tenancy;

/// <summary>
/// Every entitlement (S04 D1), in the order of docs/architecture/authorization.md's table. Tenancy owns their
/// definitions; <see cref="EntitlementDefinition.EnforcedBy"/> names the module that checks each one. Exported to
/// docs/generated/entitlements.md by the CLI.
/// </summary>
public static class TenancyEntitlements
{
    public static readonly EntitlementDefinition MaxEnabledSports = new(
        "MAX_ENABLED_SPORTS",
        "Enabled sports",
        "Sports the tenant can enable. Counts distinct sports, not variants: rugby union 15s and 7s are one sport.",
        EntitlementType.Quota,
        enforcedBy: "Tenancy & Commercial",
        since: "S04",
        unit: "sports");

    public static readonly EntitlementDefinition MaxActiveEditions = new(
        "MAX_ACTIVE_EDITIONS",
        "Active editions",
        "Competition editions in DRAFT, REGISTRATION_OPEN, REGISTRATION_CLOSED or IN_PROGRESS.",
        EntitlementType.Quota,
        enforcedBy: "Competitions",
        since: "S04",
        unit: "editions");

    public static readonly EntitlementDefinition MaxTeams = new(
        "MAX_TEAMS",
        "Teams",
        "Active teams in the registry.",
        EntitlementType.Quota,
        enforcedBy: "Registry",
        since: "S04",
        unit: "teams");

    public static readonly EntitlementDefinition MaxStaffUsers = new(
        "MAX_STAFF_USERS",
        "Staff users",
        "Users with a tenant membership (admins, managers, scorers, officials with logins), plus pending invitations: each reserves a seat. Public accounts don't count.",
        EntitlementType.Quota,
        enforcedBy: "Identity & Access",
        since: "S04",
        unit: "users");

    public static readonly EntitlementDefinition StorageGb = new(
        "STORAGE_GB",
        "Media storage",
        "Storage for uploaded media.",
        EntitlementType.Quota,
        enforcedBy: "Platform",
        since: "S04",
        unit: "GB");

    public static readonly EntitlementDefinition LiveScoring = new(
        "LIVE_SCORING",
        "Live scoring",
        "Start contests and record play-by-play as it happens.",
        EntitlementType.Boolean,
        enforcedBy: "Match Operations",
        since: "S04");

    public static readonly EntitlementDefinition OfflineScoring = new(
        "OFFLINE_SCORING",
        "Offline scoring",
        "Score on the mobile app without a connection and sync later.",
        EntitlementType.Boolean,
        enforcedBy: "Match Operations",
        since: "S04");

    public static readonly EntitlementDefinition PublicSite = new(
        "PUBLIC_SITE",
        "Public site",
        "The tenant's public site on its platform subdomain.",
        EntitlementType.Boolean,
        enforcedBy: "Tenancy & Commercial",
        since: "S04");

    public static readonly EntitlementDefinition HistoricalResults = new(
        "HISTORICAL_RESULTS",
        "Historical results",
        "Enter past results by hand.",
        EntitlementType.Boolean,
        enforcedBy: "Match Operations",
        since: "S04");

    public static readonly EntitlementDefinition Branding = new(
        "BRANDING",
        "Branding",
        "BASIC: logo, primary and secondary colour, \"Powered by\" footer. FULL: brand versions, images, footer optional.",
        EntitlementType.Level,
        enforcedBy: "Branding & Sponsorship",
        since: "S04",
        levels: ["BASIC", "FULL"]);

    public static readonly EntitlementDefinition CustomDomain = new(
        "CUSTOM_DOMAIN",
        "Custom domain",
        "Serve the public site on the tenant's own domain.",
        EntitlementType.Boolean,
        enforcedBy: "Tenancy & Commercial",
        since: "S04");

    public static readonly EntitlementDefinition Sponsorship = new(
        "SPONSORSHIP",
        "Sponsorship",
        "BASIC: up to 5 sponsor logos in a site-wide strip. FULL: campaigns, placements, scheduling, impression reports.",
        EntitlementType.Level,
        enforcedBy: "Branding & Sponsorship",
        since: "S04",
        levels: ["BASIC", "FULL"]);

    public static readonly EntitlementDefinition BulkImport = new(
        "BULK_IMPORT",
        "Bulk import",
        "REGISTRY: people, teams and venues from templates. FULL: also historical results and timelines.",
        EntitlementType.Level,
        enforcedBy: "Data Exchange",
        since: "S04",
        levels: ["REGISTRY", "FULL"]);

    public static readonly EntitlementDefinition Reports = new(
        "REPORTS",
        "Reports",
        "BASIC: CSV. ADVANCED: XLSX and PDF, scheduled.",
        EntitlementType.Level,
        enforcedBy: "Insights",
        since: "S04",
        levels: ["BASIC", "ADVANCED"]);

    public static readonly EntitlementDefinition Transfers = new(
        "TRANSFERS",
        "Transfers",
        "Player transfers between teams.",
        EntitlementType.Boolean,
        enforcedBy: "Team Operations",
        since: "S04");

    public static readonly EntitlementDefinition PublicNotifications = new(
        "PUBLIC_NOTIFICATIONS",
        "Public notifications",
        "Email and push alerts to the public. Staff notifications are never gated.",
        EntitlementType.Boolean,
        enforcedBy: "Engagement",
        since: "S04");

    public static readonly EntitlementDefinition ApiAccess = new(
        "API_ACCESS",
        "API access",
        "Tenant API keys.",
        EntitlementType.Boolean,
        enforcedBy: "Identity & Access",
        since: "S04");
}
