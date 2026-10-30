using SportsPlatform.BuildingBlocks.Actions;
using PlatformRoleCodes = SportsPlatform.Modules.Identity.Domain.PlatformRoles.PlatformRoles;

namespace SportsPlatform.Modules.Identity.Domain.Roles;

/// <summary>
/// The system roles (docs/architecture/authorization.md; S03 D9), defined in code. Each slice that adds actions adds
/// them to the roles here; tenant-custom roles come later. Exported to <c>docs/generated/roles.md</c>.
/// </summary>
public static class IdentityRoles
{
    public const string TenantOwner = "TENANT_OWNER";
    public const string TenantAdmin = "TENANT_ADMIN";
    public const string Registrar = "REGISTRAR";
    public const string CompetitionManager = "COMPETITION_MANAGER";
    public const string ResultsOfficer = "RESULTS_OFFICER";
    public const string Scorer = "SCORER";
    public const string TeamManager = "TEAM_MANAGER";
    public const string DataEntry = "DATA_ENTRY";
    public const string Viewer = "VIEWER";

    /// <summary>
    /// The roles that may work in a <c>PROVISIONING</c> tenant, held tenant-wide (S03 D11); other members get
    /// <c>TENANT_PROVISIONING</c> until it is activated.
    /// </summary>
    public static IReadOnlyList<string> SetupRoles { get; } = [TenantOwner, TenantAdmin];

    private const string _tenantSettingsRead = "tenant.settings.read";
    private const string _tenantSettingsUpdate = "tenant.settings.update";

    private const string _membershipRead = "membership.read";

    // The plan, its limits and their usage (S04 D15): those who run the tenant and its read-only admins.
    private const string _subscriptionRead = "tenant.subscription.read";

    // Every role assigned tenant-wide reads the settings (time zone, locale, currency) its screens need (owner, S03-T3)
    // and what it may do there (S03-T5). Held only below the tenant, it grants neither until requests carry resource
    // scopes (S06, #52 R1).
    private const string _capabilitiesRead = "tenant.capabilities.read";

    // Everyone working in the tenant uses the sport's words (S05 D14): every role holding the settings read.
    private const string _sportRead = "sport.read";

    private static readonly string[] _tenantAdministration =
    [
        _tenantSettingsRead, _tenantSettingsUpdate, _capabilitiesRead, _sportRead,
        _membershipRead, "membership.suspend", "membership.restore", "membership.remove", "role.assign", "role.revoke",
        "membership.invite", "invitation.revoke", _subscriptionRead,
    ];

    // Only the owner sees a blocked tenant's status and why (S03 D11): the actions available when blocked.
    private static readonly string[] _tenantOwnership = ["tenant.access.read"];

    private static readonly string[] _tenantContext = [_tenantSettingsRead, _capabilitiesRead, _sportRead];

    private static readonly string[] _platformReads =
    [
        "plan.read", "platform.audit.read", "platform.capabilities.read", "platform.outbox.read", "platform.sport.read", "subscription.read",
        "tenant.read",
    ];

    public static IReadOnlyList<RoleDefinition> All { get; } =
    [
        new(PlatformRoleCodes.Admin, RoleScope.Platform,
            "Platform staff: tenants and their lifecycle, plans, subscriptions and entitlement overrides, platform administrators, audit, outbox and sport definitions.",
            [
                .. _platformReads,
                "entitlement.override",
                "entitlement.override.remove",
                "platform.admin.create",
                "platform.outbox.redrive",
                "subscription.assign",
                "subscription.cancel",
                "subscription.change_plan",
                "subscription.renew",
                "tenant.activate",
                "tenant.archive",
                "tenant.create",
                "tenant.owner.invite",
                "tenant.restore",
                "tenant.suspend",
                "tenant.unarchive",
                "tenant.update",
            ]),
        new(PlatformRoleCodes.Support, RoleScope.Platform,
            "Platform staff, read-only: tenants, plans, subscriptions, audit, outbox and sport definitions (reads of tenants, plans, subscriptions, audit and outbox are audited).",
            _platformReads),

        new(TenantOwner, RoleScope.Tenant,
            "All tenant actions, including members, roles, subscription view and tenant settings.",
            [.. _tenantAdministration, .. _tenantOwnership], [AssignmentScope.Tenant]),
        new(TenantAdmin, RoleScope.Tenant,
            "All tenant actions except ownership transfer.",
            _tenantAdministration, [AssignmentScope.Tenant]),
        new(Registrar, RoleScope.Tenant,
            "People, organisations, teams and venues: create, update, duplicate review; no merges.",
            _tenantContext, [AssignmentScope.Tenant]),
        new(CompetitionManager, RoleScope.Tenant,
            "Competitions, editions, registrations, roster approval, stages, fixtures, officials, contest admin, standings recalculation.",
            _tenantContext, [AssignmentScope.Tenant, AssignmentScope.Competition]),
        new(ResultsOfficer, RoleScope.Tenant,
            "Result approve, reject, reopen and correct; progression overrides with a reason.",
            _tenantContext, [AssignmentScope.Tenant, AssignmentScope.Competition]),
        new(Scorer, RoleScope.Tenant,
            "Contest check-in, start, periods and clock; timeline record and correct; result submit.",
            _tenantContext, [AssignmentScope.Tenant, AssignmentScope.Competition, AssignmentScope.Contest]),

        // Assigned at a team only: tenant-wide actions such as the settings and the capabilities are not its to grant
        // (owner, S03-T3, T5); it reads its capabilities once requests carry resource scopes (S06).
        new(TeamManager, RoleScope.Tenant,
            "Team profile, memberships, staff, roster and lineup drafts and submission, registration create and submit.",
            [], [AssignmentScope.Team]),
        new(DataEntry, RoleScope.Tenant,
            "Historical results entry and import dry runs (running an import needs COMPETITION_MANAGER or TENANT_ADMIN).",
            _tenantContext, [AssignmentScope.Tenant, AssignmentScope.Competition]),
        new(Viewer, RoleScope.Tenant,
            "Read-only admin.",
            [.. _tenantContext, _membershipRead, _subscriptionRead], [AssignmentScope.Tenant]),
    ];
}
