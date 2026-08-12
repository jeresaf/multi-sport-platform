namespace SportsPlatform.Modules.Identity.Entities;

public static class SystemPermissions
{
    public static readonly IReadOnlyList<string> All =
    [
        "audit.read", "branding.manage", "branding.read",
        "competitions.create", "competitions.manage", "competitions.read",
        "contests.create", "contests.manage", "contests.read",
        "participants.create", "participants.read",
        "participants.manage-sensitive", "participants.read-sensitive",
        "notifications.manage", "notifications.read",
        "privacy.manage", "privacy.read", "reports.export", "reports.read",
        "registrations.override",
        "results.approve", "results.manage", "results.submit",
        "roles.assign", "roles.create", "roles.delete", "roles.read", "roles.update",
        "scoring.correct", "scoring.manage", "scoring.submit",
        "sponsorships.manage", "sponsorships.read",
        "sports.create", "sports.manage", "sports.read",
        "subscriptions.create", "subscriptions.manage", "subscriptions.read",
        "support.manage", "support.read",
        "tenants.delete", "tenants.manage", "tenants.read", "tenants.update",
        "users.manage", "users.read"
    ];

    private static readonly HashSet<string> Known = All.ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> PlatformOnly = new(StringComparer.Ordinal)
    {
        "sports.create", "subscriptions.create",
        "support.manage", "support.read", "tenants.delete", "tenants.manage"
    };

    public static IReadOnlyList<string> TenantAssignable { get; } =
        All.Where(permission => !PlatformOnly.Contains(permission)).ToArray();

    public static string Normalize(string permission)
    {
        var normalized = permission?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Known.Contains(normalized))
            throw new ArgumentException($"Unknown permission '{normalized}'.", nameof(permission));
        return normalized;
    }

    public static bool IsTenantAssignable(string permission) =>
        Known.Contains(permission) && !PlatformOnly.Contains(permission);

    public static bool IsKnown(string permission) => Known.Contains(permission);
}

public sealed record SystemRoleDefinition(
    string Name,
    string Description,
    bool IsPlatformRole,
    IReadOnlyList<string> Permissions);

public static class SystemRoleCatalogue
{
    private static string[] Permissions(params string[] values) => values;

    public static readonly IReadOnlyList<SystemRoleDefinition> Definitions =
    [
        new(SystemRoles.PlatformOwner, "Full platform administration.", true, SystemPermissions.All),
        new(SystemRoles.PlatformSupport, "Platform support and operational diagnostics.", true,
            Permissions("audit.read", "privacy.read", "reports.read", "reports.export",
                "roles.read", "subscriptions.read", "support.read", "support.manage", "tenants.read", "users.read")),
        new(SystemRoles.TenantOwner, "Full administration within one tenant.", false,
            SystemPermissions.TenantAssignable),
        new(SystemRoles.CompetitionAdministrator, "Competition, participant, fixture, scoring, and publication administration.", false,
            Permissions("sports.read", "competitions.read", "competitions.create", "competitions.manage",
                "contests.read", "contests.create", "contests.manage", "participants.read", "participants.create",
                "scoring.manage", "scoring.correct", "results.manage", "results.submit", "results.approve",
                "notifications.read", "notifications.manage", "reports.read", "reports.export", "sponsorships.read", "registrations.override")),
        new(SystemRoles.RegistrationOfficer, "Participant and registration administration.", false,
            Permissions("sports.read", "competitions.read", "competitions.manage", "contests.read",
                "participants.read", "participants.create",
                "participants.read-sensitive", "participants.manage-sensitive")),
        new(SystemRoles.FixtureManager, "Fixture generation and schedule administration.", false,
            Permissions("sports.read", "competitions.read", "competitions.manage",
                "contests.read", "contests.create", "contests.manage", "participants.read",
                "notifications.read", "notifications.manage")),
        new(SystemRoles.HeadOfficial, "Scoring oversight, corrections, and result approval.", false,
            Permissions("sports.read", "competitions.read", "contests.read", "participants.read",
                "scoring.manage", "scoring.submit", "scoring.correct",
                "results.manage", "results.submit", "results.approve")),
        new(SystemRoles.Scorer, "Live scoring and result submission.", false,
            Permissions("sports.read", "competitions.read", "contests.read", "contests.manage",
                "participants.read", "scoring.manage", "scoring.submit", "results.manage", "results.submit")),
        new(SystemRoles.ResultApprover, "Independent result review and approval.", false,
            Permissions("sports.read", "competitions.read", "contests.read", "participants.read",
                "scoring.correct", "results.manage", "results.approve")),
        new(SystemRoles.MediaManager, "Tenant branding, sponsors, reports, and public presentation.", false,
            Permissions("sports.read", "competitions.read", "contests.read", "participants.read",
                "branding.read", "branding.manage", "reports.read", "reports.export",
                "notifications.read", "notifications.manage", "sponsorships.read", "sponsorships.manage")),
        new(SystemRoles.Viewer, "Read-only tenant administration access.", false,
            Permissions("sports.read", "competitions.read", "contests.read", "participants.read",
                "branding.read", "notifications.read", "reports.read", "sponsorships.read"))
    ];

    public static IReadOnlyList<SystemRoleDefinition> PlatformRoles { get; } =
        Definitions.Where(role => role.IsPlatformRole).ToArray();

    public static IReadOnlyList<SystemRoleDefinition> TenantRoles { get; } =
        Definitions.Where(role => !role.IsPlatformRole).ToArray();

    public static IReadOnlyList<Role> CreateTenantRoles(Guid tenantId)
    {
        return TenantRoles.Select(definition =>
        {
            var role = new Role(
                Guid.NewGuid(), definition.Name, definition.Description,
                isSystemRole: true, tenantId: tenantId);
            foreach (var permission in definition.Permissions)
                role.AddPermission(permission);
            return role;
        }).ToArray();
    }
}
