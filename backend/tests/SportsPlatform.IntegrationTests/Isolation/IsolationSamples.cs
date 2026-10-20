using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.IntegrationTests.Messaging;
using SportsPlatform.IntegrationTests.Persistence;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Infrastructure.Users;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.IntegrationTests.Isolation;

/// <summary>
/// Builds rows of one tenant-owned entity type for <see cref="TenantIsolationTests"/>. Every <see cref="ITenantOwned"/>
/// entity of every module DbContext needs one in <see cref="IsolationSamples.All"/>; the harness fails without it.
/// </summary>
public interface IIsolationSample
{
    Type EntityType { get; }

    /// <summary>
    /// Adds a new entity, and any parents it needs, to <paramref name="context"/>; the harness saves it. Leave its tenant
    /// empty (the interceptor stamps it) unless the entity takes it from its parent, like a tenant's status history:
    /// the harness then runs under that tenant.
    /// </summary>
    Entity Add(DbContext context);

    /// <summary>
    /// A new entity that points at <paramref name="principalId"/> through <paramref name="foreignKey"/>, for each
    /// tenant-owned foreign key onto another sample's type; null if this type has no such key.
    /// </summary>
    Entity? Referencing(IForeignKey foreignKey, Guid principalId);
}

/// <summary>The samples. Add one when a module adds a tenant-owned entity (.claude/rules/testing.md).</summary>
public static class IsolationSamples
{
    public static IReadOnlyList<IIsolationSample> All { get; } =
    [
        new TenantStatusChangeSample(),
        new TenantSettingsSample(),
        new TenantProbeSample(),
        new TenantProbeNoteSample(),
        new ConsumedNoteSample(),
        new TenantMembershipSample(),
        new RoleAssignmentSample(),
    ];

    public static IIsolationSample For(Type entityType) =>
        All.SingleOrDefault(sample => sample.EntityType == entityType)
        ?? throw new InvalidOperationException($"No isolation sample for {entityType.Name}; add one to {nameof(IsolationSamples)}.");

    /// <summary>Tenancy: the history row a new tenant gets; it carries that tenant's ID.</summary>
    private sealed class TenantStatusChangeSample : IIsolationSample
    {
        public Type EntityType => typeof(TenantStatusChange);

        public Entity Add(DbContext context)
        {
            var profile = new TenantProfile("Mvule Youth Football League", "Mvule Youth", "UG");
            var tenant = Tenant.Create("mvule-youth", profile, SystemActor.Instance, new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
            context.Add(tenant);
            return tenant.StatusHistory.Single();
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) => null;
    }

    /// <summary>Tenancy: a new tenant's settings; they carry that tenant's ID, which the tenants table must have.</summary>
    private sealed class TenantSettingsSample : IIsolationSample
    {
        public Type EntityType => typeof(TenantSettings);

        public Entity Add(DbContext context)
        {
            var profile = new TenantProfile("Eastern Lakes Schools Sports Council", "ELSSC", "UG");
            var tenant = Tenant.Create("elssc", profile, SystemActor.Instance, new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
            var settings = TenantSettings.Create(tenant.Id, TenantSettingsValues.Defaults("Africa/Kampala", "en-UG", "UGX", publicEnabled: true));
            context.AddRange(tenant, settings);
            return settings;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) => null;
    }

    private sealed class TenantProbeSample : IIsolationSample
    {
        public Type EntityType => typeof(TenantProbe);

        public Entity Add(DbContext context)
        {
            var probe = new TenantProbe("Nakivubo War Memorial Stadium");
            context.Add(probe);
            return probe;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) => null;
    }

    private sealed class TenantProbeNoteSample : IIsolationSample
    {
        public Type EntityType => typeof(TenantProbeNote);

        public Entity Add(DbContext context)
        {
            var probe = new TenantProbe("Kitende Grounds");
            var note = new TenantProbeNote(probe.Id, "Pitch watered on Fridays");
            context.AddRange(probe, note);
            return note;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(TenantProbe)
                ? new TenantProbeNote(principalId, "Floodlights checked")
                : null;
    }

    private sealed class ConsumedNoteSample : IIsolationSample
    {
        public Type EntityType => typeof(ConsumedNote);

        public Entity Add(DbContext context)
        {
            var note = new ConsumedNote(Guid.CreateVersion7());
            context.Add(note);
            return note;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) => null;
    }

    /// <summary>Identity: a membership of a new (global) user; the user's key is not tenant-owned.</summary>
    private sealed class TenantMembershipSample : IIsolationSample
    {
        public Type EntityType => typeof(TenantMembership);

        public Entity Add(DbContext context) => IdentitySamples.AddMember(context, "grace.nakato@kmsa.example");

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) => null;
    }

    /// <summary>Identity: a tenant-wide role held by a new member.</summary>
    private sealed class RoleAssignmentSample : IIsolationSample
    {
        public Type EntityType => typeof(RoleAssignment);

        public Entity Add(DbContext context)
        {
            var membership = IdentitySamples.AddMember(context, "daniel.kato@kmsa.example");
            return membership.AssignRole(IdentitySamples.Admin, AssignmentScope.Tenant, scopeId: null, SystemActor.Instance, IdentitySamples.Now).Value;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(TenantMembership)
                ? RoleAssignment.Grant(principalId, IdentitySamples.Admin, AssignmentScope.Tenant, scopeId: null, SystemActor.Instance, IdentitySamples.Now)
                : null;
    }

    private static class IdentitySamples
    {
        public static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

        public static RoleDefinition Admin { get; } = IdentityRoles.All.Single(role => role.Code == IdentityRoles.TenantAdmin);

        public static TenantMembership AddMember(DbContext context, string email)
        {
            var user = User.Create(email, emailConfirmed: true);
            var membership = TenantMembership.Join(user.Id, invitedBy: null, Now);
            context.AddRange(user, membership);
            return membership;
        }
    }
}
