using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Identity.Entities;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Tenancy.Entities;

namespace SportsPlatform.Infrastructure.Persistence.SeedData;

/// <summary>
/// Creates only the platform reference catalogue and the bootstrap administrator.
/// It deliberately contains no demo tenants, users, competitions, participants,
/// fixtures, scores, or results so the complete administration journey can be
/// exercised from a clean database.
/// </summary>
public static class WalkthroughSeedData
{
    private static readonly Guid DefaultTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PlatformOwnerRoleId =
        Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid AdministratorId =
        Guid.Parse("00000000-0000-0000-0000-000000000020");

    public static async Task EnsureAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken = default)
    {
        if (await context.Tenants.AnyAsync(cancellationToken))
            return;

        var defaultTenant = new Tenant(
            DefaultTenantId,
            "Platform Administration",
            "Platform",
            "default");
        defaultTenant.UpdateDetails(
            "Platform Administration",
            "Platform",
            "Bootstrap tenant used only by the platform administrator.");
        context.Tenants.Add(defaultTenant);

        var platformOwner = new Role(
            PlatformOwnerRoleId,
            SystemRoles.PlatformOwner,
            "Full platform administration.",
            isSystemRole: true);
        foreach (var permission in SystemPermissions.All)
            platformOwner.AddPermission(permission);
        context.Roles.Add(platformOwner);

        var administrator = new User(
            AdministratorId,
            "admin@sportsplatform.com",
            "Platform",
            "Administrator",
            "Admin123!",
            DefaultTenantId);
        context.Users.Add(administrator);
        context.UserRoles.Add(new UserRole(
            Guid.Parse("00000000-0000-0000-0000-000000000030"),
            AdministratorId,
            PlatformOwnerRoleId));

        context.Sports.AddRange(SportCatalogue());
        await context.SaveChangesAsync(cancellationToken);
        await SeedData.EnsurePublishedRulesetsAsync(context);
    }

    private static IReadOnlyList<Sport> SportCatalogue()
    {
        return
        [
            Sport("00000000-0000-0000-0000-000000000100", "FOOTBALL", "Football",
                "Association football", "⚽", ["team"]),
            Sport("00000000-0000-0000-0000-000000000101", "BASKETBALL", "Basketball",
                "Basketball", "🏀", ["team"]),
            Sport("00000000-0000-0000-0000-000000000102", "GOLF", "Golf",
                "Stroke and match play golf", "⛳", ["individual", "team"]),
            Sport("00000000-0000-0000-0000-000000000103", "ATHLETICS", "Athletics",
                "Track and field", "🏃", ["individual", "team"]),
            Sport("00000000-0000-0000-0000-000000000104", "CRICKET", "Cricket",
                "Cricket", "🏏", ["team"]),
            Sport("00000000-0000-0000-0000-000000000105", "MMA", "Mixed Martial Arts",
                "Combat sports", "🥊", ["individual"]),
            Sport("00000000-0000-0000-0000-000000000106", "RUGBY", "Rugby",
                "Rugby 7s and Rugby 15s", "🏉", ["team"]),
            Sport("00000000-0000-0000-0000-000000000107", "VOLLEYBALL", "Volleyball",
                "Indoor and beach volleyball", "🏐", ["team"]),
            Sport("00000000-0000-0000-0000-000000000108", "NETBALL", "Netball",
                "Netball", "🎯", ["team"]),
            Sport("00000000-0000-0000-0000-000000000109", "HOCKEY", "Hockey",
                "Field hockey", "🏑", ["team"]),
            Sport("00000000-0000-0000-0000-000000000110", "TENNIS", "Tennis",
                "Singles and doubles tennis", "🎾", ["individual", "team"]),
            Sport("00000000-0000-0000-0000-000000000111", "BOXING", "Boxing",
                "Boxing", "🥊", ["individual"])
        ];
    }

    private static Sport Sport(
        string id,
        string code,
        string name,
        string description,
        string icon,
        string[] participantTypes)
    {
        var sport = new Sport(Guid.Parse(id), code, name);
        sport.UpdateDetails(name, description, icon, participantTypes);
        return sport;
    }
}
