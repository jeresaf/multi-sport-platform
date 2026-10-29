using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Infrastructure;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>Tenant memberships and role assignments, saved under their tenant as the system actor.</summary>
internal static class MembershipTestData
{
    public static readonly DateTimeOffset JoinedAt = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A test-only role granting the test action <c>fixture.read</c> (TENANT, COMPETITION) at three scope types.</summary>
    public const string FixtureReader = "FIXTURE_READER";

    /// <summary>A test-only role granting S04 D20's test actions <c>fixture.publish</c> and <c>fixture.report.read</c> tenant-wide.</summary>
    public const string FixturePublisher = "FIXTURE_PUBLISHER";

    /// <summary>The application's roles, <see cref="FixtureReader"/> and <see cref="FixturePublisher"/>.</summary>
    public static RoleCatalogue Roles { get; } = new(
    [
        .. IdentityRoles.All,
        new RoleDefinition(FixtureReader, RoleScope.Tenant, "Reads fixtures.", ["fixture.read"],
            [AssignmentScope.Tenant, AssignmentScope.Competition, AssignmentScope.Team]),
        new RoleDefinition(FixturePublisher, RoleScope.Tenant, "Publishes fixtures.", ["fixture.publish", "fixture.report.read"], [AssignmentScope.Tenant]),
    ]);

    /// <summary>A role held tenant-wide.</summary>
    public static (string Role, AssignmentScope Scope, Guid? ScopeId) TenantWide(string role) => (role, AssignmentScope.Tenant, null);

    /// <summary>A mailbox named after a role, e.g. <c>tenant.owner@kmsa.example</c>, for table-driven tests over roles.</summary>
    public static string MailboxOf(string role, string domain) => $"{role.ToLowerInvariant().Replace('_', '.')}@{domain}";

    /// <summary>A new user, an active member of <paramref name="tenantId"/> with <paramref name="roles"/>.</summary>
    public static async Task<Member> CreateMemberAsync(
        IServiceProvider services, Guid tenantId, string email, CancellationToken cancellationToken,
        params (string Role, AssignmentScope Scope, Guid? ScopeId)[] roles)
    {
        var userId = await AccountTestData.CreateUserAsync(services, email, TestPasswords.New(), cancellationToken);
        var membershipId = await JoinAsync(services, tenantId, userId, cancellationToken, roles);
        return new Member(userId, membershipId, tenantId);
    }

    /// <summary>An existing user joins <paramref name="tenantId"/> with <paramref name="roles"/>; returns the membership ID.</summary>
    public static async Task<Guid> JoinAsync(
        IServiceProvider services, Guid tenantId, Guid userId, CancellationToken cancellationToken,
        params (string Role, AssignmentScope Scope, Guid? ScopeId)[] roles)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        var membership = TenantMembership.Join(userId, invitedBy: null, JoinedAt);
        foreach (var (role, scopeType, scopeId) in roles)
        {
            var definition = Roles.Find(role) ?? throw new InvalidOperationException($"Unknown role {role}.");
            membership.AssignRole(definition, scopeType, scopeId, SystemActor.Instance, JoinedAt).IsSuccess.ShouldBeTrue(role);
        }

        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        context.Add(membership);
        (await context.SaveChangesResultAsync(cancellationToken)).IsSuccess.ShouldBeTrue();
        return membership.Id;
    }

    /// <summary>Loads the member's membership with its roles, applies <paramref name="change"/> and saves.</summary>
    public static async Task ChangeAsync(
        IServiceProvider services, Member member, Func<TenantMembership, BuildingBlocks.Results.Result> change, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(member.TenantId);
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var membership = await context.Set<TenantMembership>().Include(m => m.Roles).SingleAsync(m => m.Id == member.MembershipId, cancellationToken);
        change(membership).IsSuccess.ShouldBeTrue();
        (await context.SaveChangesResultAsync(cancellationToken)).IsSuccess.ShouldBeTrue();
    }
}

/// <summary>A user and their membership of a tenant.</summary>
internal sealed record Member(Guid UserId, Guid MembershipId, Guid TenantId)
{
    public ICurrentActor Actor => new UserActor(UserId);
}
