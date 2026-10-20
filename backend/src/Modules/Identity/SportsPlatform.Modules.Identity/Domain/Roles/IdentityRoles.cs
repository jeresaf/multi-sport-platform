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

    // Every role assigned tenant-wide reads the settings (time zone, locale, currency) its screens need (owner, S03-T3).
    private const string _membershipRead = "membership.read";

    private static readonly string[] _tenantAdministration =
    [
        _tenantSettingsRead, _tenantSettingsUpdate,
        _membershipRead, "membership.suspend", "membership.restore", "membership.remove", "role.assign", "role.revoke",
        "membership.invite", "invitation.revoke",
    ];

    // Only the owner sees a blocked tenant's status and why (S03 D11): the actions available when blocked.
    private static readonly string[] _tenantOwnership = ["tenant.access.read"];

    private static readonly string[] _tenantContext = [_tenantSettingsRead];

    private static readonly string[] _platformReads = ["platform.audit.read", "platform.outbox.read", "tenant.read"];

    public static IReadOnlyList<RoleDefinition> All { get; } =
    [
        new(PlatformRoleCodes.Admin, RoleScope.Platform,
            "Platform staff: tenants and their lifecycle, platform administrators, audit and outbox.",
            [
                .. _platformReads,
                "platform.admin.create",
                "platform.outbox.redrive",
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
            "Platform staff, read-only: tenants, audit and outbox (every read is audited).",
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

        // Assigned at a team only: tenant-wide actions such as the settings are not its to grant (owner, S03-T3).
        new(TeamManager, RoleScope.Tenant,
            "Team profile, memberships, staff, roster and lineup drafts and submission, registration create and submit.",
            [], [AssignmentScope.Team]),
        new(DataEntry, RoleScope.Tenant,
            "Historical results entry and import dry runs (running an import needs COMPETITION_MANAGER or TENANT_ADMIN).",
            _tenantContext, [AssignmentScope.Tenant, AssignmentScope.Competition]),
        new(Viewer, RoleScope.Tenant,
            "Read-only admin.",
            [.. _tenantContext, _membershipRead], [AssignmentScope.Tenant]),
    ];
}
