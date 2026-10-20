using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Features.Invitations;
using SportsPlatform.Modules.Identity.Features.Invitations.InviteMember;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Identity.Seed.Demo;

/// <summary>
/// The pilot customer's people (docs/product/pilot-customer.md, S03 D18): each is invited to their demo tenant with
/// their role, through <c>membership.invite</c> as the system actor (audited <c>SYSTEM</c>). No passwords: the
/// invitations are emailed by Identity's outbox consumer when the API runs, and whoever runs the demo accepts them.
/// Idempotent by tenant and email: a person with a pending (unexpired) or accepted invitation, or a current
/// membership, is left alone; an expired or revoked invitation is sent again. Only an <c>ACTIVE</c> tenant is seeded.
/// Invitations get no deterministic IDs: a person may have several over time.
/// </summary>
public sealed class DemoInvitationSeeder : ISeeder
{
    /// <summary>Bump when the seeder writes something different from the same data.</summary>
    private const int _version = 1;

    internal static readonly IReadOnlyList<DemoInvitee> Invitees =
    [
        new("kmsa", "Grace Nakato", "grace.nakato@kmsa.example", IdentityRoles.TenantOwner),
        new("kmsa", "Daniel Kato", "daniel.kato@kmsa.example", IdentityRoles.TenantAdmin),
        new("kmsa", "Brian Ssemakula", "brian.ssemakula@kmsa.example", IdentityRoles.Registrar),
        new("kmsa", "Esther Nabirye", "esther.nabirye@kmsa.example", IdentityRoles.ResultsOfficer),
        new("elssc", "Agnes Namutebi", "agnes.namutebi@elssc.example", IdentityRoles.TenantOwner),
    ];

    public string Key => "identity.demo.invitations";

    public SeedCategory Category => SeedCategory.Demo;

    /// <summary>After <c>tenancy.demo.tenants</c> (0), which creates and activates the tenants.</summary>
    public int Order => 10;

    public string Checksum { get; } = ComputeChecksum();

    public async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tenants = context.Services.GetRequiredService<ITenantDirectory>();
        var inserted = 0;
        foreach (var slug in Invitees.Select(i => i.TenantSlug).Distinct(StringComparer.Ordinal))
        {
            var found = await tenants.GetBySlugAsync(slug, cancellationToken);
            if (found.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Demo tenant '{slug}' doesn't exist: tenancy.demo.tenants runs first and creates it ({found.Error.Code}).");
            }

            var tenant = found.Value;
            if (tenant.Status != TenantStatus.Active)
            {
                // As tenancy.demo.tenants keeps a status someone chose, the people of a suspended or archived tenant wait.
                continue;
            }

            foreach (var invitee in Invitees.Where(i => i.TenantSlug == slug))
            {
                if (await context.QueryAsTenantAsync(tenant.Id, (services, ct) => IsSettledAsync(services, invitee, ct), cancellationToken))
                {
                    continue;
                }

                await context.RunCommandAsync(
                    IdentityActions.MembershipInvite, tenant.Id, (services, ct) => InviteAsync(services, invitee, ct), cancellationToken);
                inserted++;
            }
        }

        return new SeedOutcome(inserted, Updated: 0, Deactivated: 0);
    }

    /// <summary>
    /// Whether the person needs no invitation: one pending and unexpired, one accepted (even if the member was removed
    /// since: D18 sends neither again), or the email's user already a member. A pending invitation counts whatever its
    /// roles: the seeder doesn't replace one someone sent by hand.
    /// </summary>
    private static async Task<bool> IsSettledAsync(IServiceProvider services, DemoInvitee invitee, CancellationToken cancellationToken)
    {
        var context = services.GetRequiredService<IdentityDbContext>();
        var normalized = services.GetRequiredService<ILookupNormalizer>().NormalizeEmail(invitee.Email);
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        if (await context.Set<Invitation>().AnyAsync(
                i => i.NormalizedEmail == normalized
                     && (i.Status == InvitationStatus.Accepted || (i.Status == InvitationStatus.Pending && i.ExpiresAt > now)),
                cancellationToken))
        {
            return true;
        }

        // Users are global; memberships are the tenant's (RLS).
        return await context.Set<User>().Where(u => u.NormalizedEmail == normalized)
            .Join(context.Set<TenantMembership>(), u => u.Id, m => m.UserId, (_, m) => m)
            .AnyAsync(m => m.Status != MembershipStatus.Removed, cancellationToken);
    }

    private static async Task<bool> InviteAsync(IServiceProvider services, DemoInvitee invitee, CancellationToken cancellationToken)
    {
        var request = new InviteMemberRequest(invitee.Email, [invitee.RoleCode]);

        // As the endpoint does: handlers expect a validated request.
        var validation = await services.GetRequiredService<IValidator<InviteMemberRequest>>().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Demo invitation data for {invitee.Name} is invalid: {validation}");
        }

        Succeed(await services.GetRequiredService<InviteHandler>().InviteMemberAsync(request, cancellationToken), invitee.Name);
        return true;
    }

    private static void Succeed<T>(Result<T> result, string who)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Seeding the demo invitation of {who} failed: {result.Error.Code} {result.Error.Message}");
        }
    }

    private static string ComputeChecksum()
    {
        var data = JsonSerializer.Serialize(new { version = _version, invitees = Invitees });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
    }
}

/// <summary>A demo person invited to a demo tenant (by slug) with one tenant-wide role.</summary>
internal sealed record DemoInvitee(string TenantSlug, string Name, string Email, string RoleCode);
