using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.IntegrationTests.Messaging;
using SportsPlatform.IntegrationTests.Persistence;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Infrastructure.Users;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Sports;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
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
        new SubscriptionPeriodSample(),
        new EntitlementOverrideSample(),
        new TenantSportSample(),
        new TenantProbeSample(),
        new TenantProbeNoteSample(),
        new TenantProbeNoteMarkSample(),
        new ConsumedNoteSample(),
        new TenantMembershipSample(),
        new RoleAssignmentSample(),
        new InvitationSample(),
        new PersonSample(),
        new PersonAliasSample(),
        new PersonStatusChangeSample(),
        new ExternalIdentifierSample(),
        new DuplicateDecisionSample(),
        new AthleteProfileSample(),
        new ProfileStatusChangeSample(),
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

    /// <summary>
    /// Tenancy: a period of a new tenant. The tenants table must have its tenant, so the sample gives it the new tenant's
    /// ID (as the interceptor would when saved under that tenant).
    /// </summary>
    private sealed class SubscriptionPeriodSample : IIsolationSample
    {
        public Type EntityType => typeof(SubscriptionPeriod);

        public Entity Add(DbContext context)
        {
            var profile = new TenantProfile("Kampala Metro Sports Association", "KMSA", "UG");
            var tenant = Tenant.Create("kmsa", profile, SystemActor.Instance, new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
            var period = SubscriptionPeriod.Create(
                "STANDARD", SubscriptionKind.Complimentary, new DateOnly(2026, 10, 1), new DateOnly(2027, 3, 31), 30, 14);
            context.AddRange(tenant, period);
            context.Entry(period).Property(p => p.TenantId).CurrentValue = tenant.Id;
            return period;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) => null;
    }

    private sealed class EntitlementOverrideSample : IIsolationSample
    {
        public Type EntityType => typeof(EntitlementOverride);

        public Entity Add(DbContext context)
        {
            var profile = new TenantProfile("Eastern Lakes Schools Sports Council", "ELSSC", "UG");
            var tenant = Tenant.Create("elssc", profile, SystemActor.Instance, new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
            var entitlementOverride = EntitlementOverride.Create(
                "MAX_ACTIVE_EDITIONS", EntitlementValue.Quota(1), "Event pass: one edition", new DateOnly(2026, 11, 14));
            context.AddRange(tenant, entitlementOverride);
            context.Entry(entitlementOverride).Property(o => o.TenantId).CurrentValue = tenant.Id;
            return entitlementOverride;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) => null;
    }

    private sealed class TenantSportSample : IIsolationSample
    {
        public Type EntityType => typeof(TenantSport);

        public Entity Add(DbContext context)
        {
            var profile = new TenantProfile("Eastern Lakes Schools Sports Council", "ELSSC", "UG");
            var now = new DateTimeOffset(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
            var tenant = Tenant.Create("elssc", profile, SystemActor.Instance, now);
            var sport = TenantSport.Enable("FOOTBALL_ASSOCIATION", SystemActor.Instance, now);
            context.AddRange(tenant, sport);
            context.Entry(sport).Property(s => s.TenantId).CurrentValue = tenant.Id;
            return sport;
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

    private sealed class TenantProbeNoteMarkSample : IIsolationSample
    {
        public Type EntityType => typeof(TenantProbeNoteMark);

        public Entity Add(DbContext context)
        {
            var probe = new TenantProbe("Seeta Hill Sports Field");
            var note = new TenantProbeNote(probe.Id, "Nets replaced");
            var mark = new TenantProbeNoteMark(note.Id, "Checked");
            context.AddRange(probe, note, mark);
            return mark;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(TenantProbeNote)
                ? new TenantProbeNoteMark(principalId, "Urgent")
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

    /// <summary>
    /// Identity: a pending invitation; nothing tenant-owned points at one. An accepted one points at the membership it
    /// created.
    /// </summary>
    private sealed class InvitationSample : IIsolationSample
    {
        public Type EntityType => typeof(Invitation);

        public Entity Add(DbContext context)
        {
            var invitation = Invitation.Create(
                "esther.nabirye@kmsa.example", "ESTHER.NABIRYE@KMSA.EXAMPLE", [IdentitySamples.Admin], SystemActor.Instance, IdentitySamples.Now).Value;
            context.Add(invitation);
            return invitation;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId)
        {
            if (foreignKey.PrincipalEntityType.ClrType != typeof(TenantMembership))
            {
                return null;
            }

            var invitation = Invitation.Create(
                "daniel.kato@kmsa.example", "DANIEL.KATO@KMSA.EXAMPLE", [IdentitySamples.Admin], SystemActor.Instance, IdentitySamples.Now).Value;
            invitation.Accept(Guid.CreateVersion7(), principalId, IdentitySamples.Now).IsSuccess.ShouldBeTrue();
            return invitation;
        }
    }

    /// <summary>Registry: a person; its aliases and status history point at it.</summary>
    private sealed class PersonSample : IIsolationSample
    {
        public Type EntityType => typeof(Person);

        public Entity Add(DbContext context)
        {
            var person = RegistrySamples.NewPerson("Brenda", "Akello");
            context.Add(person);
            return person;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) => null;
    }

    private sealed class PersonAliasSample : IIsolationSample
    {
        public Type EntityType => typeof(PersonAlias);

        public Entity Add(DbContext context)
        {
            var person = RegistrySamples.NewPerson("Peter", "Ochieng");
            context.Add(person);
            return person.AddAlias("Peter Okot", AliasKind.FormerName).Value;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Person)
                ? new PersonAlias(principalId, "Pito", AliasKind.Nickname)
                : null;
    }

    /// <summary>Registry: the history row a new person gets.</summary>
    private sealed class PersonStatusChangeSample : IIsolationSample
    {
        public Type EntityType => typeof(PersonStatusChange);

        public Entity Add(DbContext context)
        {
            var person = RegistrySamples.NewPerson("Ivan", "Mukasa");
            context.Add(person);
            return person.StatusHistory.Single();
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Person)
                ? new PersonStatusChange(principalId, PersonStatus.Active, PersonStatus.Archived, "Left the association", SystemActor.Instance, RegistrySamples.Now)
                : null;
    }

    /// <summary>Registry: an athlete profile points at its person.</summary>
    private sealed class AthleteProfileSample : IIsolationSample
    {
        public Type EntityType => typeof(AthleteProfile);

        public Entity Add(DbContext context)
        {
            var person = RegistrySamples.NewPerson("Denis", "Opoka");
            context.Add(person);
            return person.AddAthleteProfile("TESTBALL_FULL", RegistrySamples.Midfielder, SystemActor.Instance, RegistrySamples.Now).Value;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Person)
                ? new AthleteProfile(principalId, "TESTBALL_FULL", RegistrySamples.Midfielder)
                : null;
    }

    /// <summary>Registry: the history row a new profile gets; it points at the person.</summary>
    private sealed class ProfileStatusChangeSample : IIsolationSample
    {
        public Type EntityType => typeof(ProfileStatusChange);

        public Entity Add(DbContext context)
        {
            var person = RegistrySamples.NewPerson("Shamim", "Nankya");
            context.Add(person);
            person.AddAthleteProfile("TESTBALL_FULL", RegistrySamples.Midfielder, SystemActor.Instance, RegistrySamples.Now).IsSuccess.ShouldBeTrue();
            return person.ProfileStatusHistory.Single();
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Person)
                ? new ProfileStatusChange(
                    principalId, ProfileKind.Athlete, Guid.CreateVersion7(), ProfileStatus.Active, ProfileStatus.Retired, "Retired",
                    SystemActor.Instance, RegistrySamples.Now)
                : null;
    }

    /// <summary>Registry: an identifier names its person by ID without a foreign key, so nothing tenant-owned is referenced.</summary>
    private sealed class ExternalIdentifierSample : IIsolationSample
    {
        public Type EntityType => typeof(ExternalIdentifier);

        public Entity Add(DbContext context)
        {
            var person = RegistrySamples.NewPerson("Mariam", "Nankya");
            var identifier = ExternalIdentifier.ForPerson(person.Id, "KMSA_REG_NO", "KM-2026-0147");
            context.AddRange(person, identifier);
            return identifier;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) => null;
    }

    /// <summary>Registry: a decision about two people of the tenant; neither may be another tenant's.</summary>
    private sealed class DuplicateDecisionSample : IIsolationSample
    {
        public Type EntityType => typeof(DuplicateDecision);

        public Entity Add(DbContext context)
        {
            var peter = RegistrySamples.NewPerson("Peter", "Ochieng");
            var other = RegistrySamples.NewPerson("Peter", "Okot");
            var decision = DuplicateDecision.NotSame(peter.Id, other.Id, "Cousins at different clubs", SystemActor.Instance, RegistrySamples.Now);
            context.AddRange(peter, other, decision);
            return decision;
        }

        // The principal sits in the column of the key under test: the other ID is made to sort after or before it.
        // (That other person doesn't exist either; the harness can add one row only, and both keys are checked.)
        public Entity? Referencing(IForeignKey foreignKey, Guid principalId)
        {
            if (foreignKey.PrincipalEntityType.ClrType != typeof(Person))
            {
                return null;
            }

            var principalIsLow = foreignKey.Properties.Any(property => property.Name == nameof(DuplicateDecision.PersonLowId));
            var other = Convert.FromHexString(principalId.ToString("N"));
            other[0] = principalIsLow ? (byte)0xff : (byte)0x00;
            return DuplicateDecision.NotSame(
                principalId, new Guid(Convert.ToHexString(other)), "Cousins at different clubs", SystemActor.Instance, RegistrySamples.Now);
        }
    }

    private static class RegistrySamples
    {
        public static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

        public static Person NewPerson(string firstName, string lastName) =>
            Person.Create(new PersonBiodata(firstName, lastName), PersonSource.Manual, SystemActor.Instance, Now);

        public static AthleteProfileDetails Midfielder { get; } = new("TB_MID", [], DominantSide.Left, null);
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
