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
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Domain.Media;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Sports;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using AgeStatus = SportsPlatform.Modules.Registry.Contracts.People.AgeStatus;

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
        new StaffProfileSample(),
        new StaffCapabilitySample(),
        new OfficialProfileSample(),
        new OfficialCapabilitySample(),
        new GuardianConsentSample(),
        new VenueSample(),
        new VenueStatusChangeSample(),
        new VenueAliasSample(),
        new OrganisationSample(),
        new OrganisationStatusChangeSample(),
        new OrganisationParentLinkSample(),
        new OrganisationAliasSample(),
        new TeamSample(),
        new TeamStatusChangeSample(),
        new MediaAssetSample(),
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

    /// <summary>Registry: a staff profile points at its person.</summary>
    private sealed class StaffProfileSample : IIsolationSample
    {
        public Type EntityType => typeof(StaffProfile);

        public Entity Add(DbContext context)
        {
            var person = RegistrySamples.NewPerson("Denis", "Opoka");
            context.Add(person);
            return person.AddStaffProfile([RegistrySamples.Coach], SystemActor.Instance, RegistrySamples.Now).Value;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Person)
                ? new StaffProfile(principalId, [RegistrySamples.Coach])
                : null;
    }

    /// <summary>Registry: a staff capability points at its staff profile.</summary>
    private sealed class StaffCapabilitySample : IIsolationSample
    {
        public Type EntityType => typeof(StaffCapability);

        public Entity Add(DbContext context)
        {
            var person = RegistrySamples.NewPerson("Moses", "Kato");
            context.Add(person);
            return person.AddStaffProfile([RegistrySamples.Coach], SystemActor.Instance, RegistrySamples.Now).Value.Capabilities.Single();
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(StaffProfile)
                ? new StaffCapability(principalId, RegistrySamples.Coach)
                : null;
    }

    /// <summary>Registry: an official profile points at its person.</summary>
    private sealed class OfficialProfileSample : IIsolationSample
    {
        public Type EntityType => typeof(OfficialProfile);

        public Entity Add(DbContext context)
        {
            var person = RegistrySamples.NewPerson("Denis", "Opoka");
            context.Add(person);
            return person.AddOfficialProfile([RegistrySamples.Referee], SystemActor.Instance, RegistrySamples.Now).Value;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Person)
                ? new OfficialProfile(principalId, [RegistrySamples.Referee])
                : null;
    }

    /// <summary>Registry: an official capability points at its official profile.</summary>
    private sealed class OfficialCapabilitySample : IIsolationSample
    {
        public Type EntityType => typeof(OfficialCapability);

        public Entity Add(DbContext context)
        {
            var person = RegistrySamples.NewPerson("Moses", "Kato");
            context.Add(person);
            return person.AddOfficialProfile([RegistrySamples.Referee], SystemActor.Instance, RegistrySamples.Now).Value.Capabilities.Single();
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(OfficialProfile)
                ? new OfficialCapability(principalId, RegistrySamples.Referee)
                : null;
    }

    /// <summary>Registry: a guardian's consent points at its person, and at the consent it replaced.</summary>
    private sealed class GuardianConsentSample : IIsolationSample
    {
        public Type EntityType => typeof(GuardianConsent);

        public Entity Add(DbContext context)
        {
            var person = RegistrySamples.NewPerson("Brenda", "Akello");
            context.Add(person);
            return person.RecordConsent(RegistrySamples.MotherConsent, AgeStatus.Unknown, SystemActor.Instance, RegistrySamples.Now).Value;
        }

        // A key onto the replaced consent: the person is made up, as for a duplicate decision (the harness adds one row,
        // and each key is checked).
        public Entity? Referencing(IForeignKey foreignKey, Guid principalId)
        {
            if (foreignKey.PrincipalEntityType.ClrType == typeof(Person))
            {
                return new GuardianConsent(principalId, RegistrySamples.MotherConsent, null, null, SystemActor.Instance, RegistrySamples.Now);
            }

            return foreignKey.PrincipalEntityType.ClrType == typeof(GuardianConsent)
                ? new GuardianConsent(Guid.CreateVersion7(), RegistrySamples.MotherConsent, null, principalId, SystemActor.Instance, RegistrySamples.Now)
                : null;
        }
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

    /// <summary>Registry: a venue; its status history points at it.</summary>
    private sealed class VenueSample : IIsolationSample
    {
        public Type EntityType => typeof(Venue);

        public Entity Add(DbContext context)
        {
            var venue = RegistrySamples.NewVenue("Kiwatule Meadow Ground");
            context.Add(venue);
            return venue;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) => null;
    }

    /// <summary>Registry: the history row a new venue gets.</summary>
    private sealed class VenueStatusChangeSample : IIsolationSample
    {
        public Type EntityType => typeof(VenueStatusChange);

        public Entity Add(DbContext context)
        {
            var venue = RegistrySamples.NewVenue("Bukoto Community Stadium");
            context.Add(venue);
            return venue.StatusHistory.Single();
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Venue)
                ? new VenueStatusChange(principalId, VenueStatus.Active, VenueStatus.Closed, "Pitch resurfacing", SystemActor.Instance, RegistrySamples.Now)
                : null;
    }

    /// <summary>Registry: an alias of a venue; it points at its venue.</summary>
    private sealed class VenueAliasSample : IIsolationSample
    {
        public Type EntityType => typeof(VenueAlias);

        public Entity Add(DbContext context)
        {
            var venue = RegistrySamples.NewVenue("Nakawa Lakeside Field");
            context.Add(venue);
            return venue.AddAlias("Nakawa Grounds", AliasKind.FormerName).Value;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Venue)
                ? new VenueAlias(principalId, "The Lakeside", AliasKind.Nickname)
                : null;
    }

    /// <summary>Registry: an organisation; it points at its primary venue, and its status history points at it.</summary>
    private sealed class OrganisationSample : IIsolationSample
    {
        public Type EntityType => typeof(Organisation);

        public Entity Add(DbContext context)
        {
            var organisation = RegistrySamples.NewOrganisation("Ntinda Swifts SC");
            context.Add(organisation);
            return organisation;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Venue)
                ? RegistrySamples.NewOrganisation("Kiwatule Athletic Club", primaryVenueId: principalId)
                : null;
    }

    /// <summary>Registry: the history row a new organisation gets.</summary>
    private sealed class OrganisationStatusChangeSample : IIsolationSample
    {
        public Type EntityType => typeof(OrganisationStatusChange);

        public Entity Add(DbContext context)
        {
            var organisation = RegistrySamples.NewOrganisation("Bunga Lakeside Sports Club");
            context.Add(organisation);
            return organisation.StatusHistory.Single();
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Organisation)
                ? new OrganisationStatusChange(
                    principalId, OrganisationStatus.Active, OrganisationStatus.Suspended, "Unpaid league fees", SystemActor.Instance, RegistrySamples.Now)
                : null;
    }

    /// <summary>Registry: a club's link to the association it belongs to (S07 D5).</summary>
    private sealed class OrganisationParentLinkSample : IIsolationSample
    {
        public Type EntityType => typeof(OrganisationParentLink);

        public Entity Add(DbContext context)
        {
            var association = RegistrySamples.NewOrganisation("Kampala Metro Sports Association");
            var club = RegistrySamples.NewOrganisation("Bunga Lakeside Sports Club");
            var link = new OrganisationParentLink(
                club.Id, association.Id, new DateOnly(2026, 10, 1), "Affiliated this season", SystemActor.Instance, RegistrySamples.Now);
            context.AddRange(association, club, link);
            return link;
        }

        // Both ends are organisations. The sample can add only the link, so the end not under test is a new ID that names
        // no organisation; the end under test points at tenant A's row.
        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType != typeof(Organisation)
                ? null
                : foreignKey.Properties.Any(property => property.Name == nameof(OrganisationParentLink.ParentId))
                    ? new OrganisationParentLink(
                        Guid.CreateVersion7(), principalId, new DateOnly(2026, 10, 1), "Affiliated this season", SystemActor.Instance, RegistrySamples.Now)
                    : new OrganisationParentLink(
                        principalId, Guid.CreateVersion7(), new DateOnly(2026, 10, 1), "Affiliated this season", SystemActor.Instance, RegistrySamples.Now);
    }

    /// <summary>Registry: an alias of an organisation; it points at its organisation.</summary>
    private sealed class OrganisationAliasSample : IIsolationSample
    {
        public Type EntityType => typeof(OrganisationAlias);

        public Entity Add(DbContext context)
        {
            var organisation = RegistrySamples.NewOrganisation("Kisaasi Kestrels SC");
            context.Add(organisation);
            return organisation.AddAlias("Kisaasi Kestrels Sports Club", AliasKind.FormerName).Value;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Organisation)
                ? new OrganisationAlias(principalId, "The Kestrels", AliasKind.Nickname)
                : null;
    }

    /// <summary>Registry: a team; it points at its organisation and its home venue, and its status history points at it.</summary>
    private sealed class TeamSample : IIsolationSample
    {
        public Type EntityType => typeof(Team);

        public Entity Add(DbContext context)
        {
            var team = RegistrySamples.NewTeam("Ntinda Swifts FC");
            context.Add(team);
            return team;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Organisation)
                ? RegistrySamples.NewTeam("Kiwatule Athletic FC", organisationId: principalId)
                : foreignKey.PrincipalEntityType.ClrType == typeof(Venue)
                    ? RegistrySamples.NewTeam("Kisaasi Kestrels FC", homeVenueId: principalId)
                    : null;
    }

    /// <summary>Registry: the history row a new team gets.</summary>
    private sealed class TeamStatusChangeSample : IIsolationSample
    {
        public Type EntityType => typeof(TeamStatusChange);

        public Entity Add(DbContext context)
        {
            var team = RegistrySamples.NewTeam("Seeta Sparrows FC");
            context.Add(team);
            return team.StatusHistory.Single();
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Team)
                ? new TeamStatusChange(principalId, TeamStatus.Active, TeamStatus.Disbanded, "Folded into the first team", SystemActor.Instance, RegistrySamples.Now)
                : null;
    }

    private static class RegistrySamples
    {
        public static Team NewTeam(string name, Guid? organisationId = null, Guid? homeVenueId = null)
        {
            var category = new TeamCategory(GenderCategory.Men, AgeGroup.Senior, TeamLevel.First);
            return Team.Create(
                "TESTBALL_FULL",
                new TeamDetails(name, null, category, organisationId, null, null, null, null, null, homeVenueId, null),
                new DateOnly(2020, 1, 1),
                Team.ProposeSlug(name, category),
                TeamSource.Manual,
                SystemActor.Instance,
                Now);
        }

        public static Organisation NewOrganisation(string name, Guid? primaryVenueId = null) =>
            Organisation.Create(
                new OrganisationDetails(OrganisationType.Club, name, null, null, "UG", "Kampala", null, null, null, null, null, primaryVenueId),
                Slugs.Propose("organisation", name),
                OrganisationSource.Manual,
                SystemActor.Instance,
                Now);

        public static Venue NewVenue(string name) =>
            Venue.Create(
                new VenueDetails(name, null, VenueType.SportsGround, null, "Kampala", null, "UG", null, "Africa/Kampala", null, [], []),
                Slugs.Propose("venue", name),
                VenueSource.Manual,
                SystemActor.Instance,
                Now);

        public static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

        public static Person NewPerson(string firstName, string lastName) =>
            Person.Create(new PersonBiodata(firstName, lastName), PersonSource.Manual, SystemActor.Instance, Now);

        public static AthleteProfileDetails Midfielder { get; } = new("TB_MID", [], DominantSide.Left, null);

        public static StaffCapabilityDetails Coach { get; } = new("TESTBALL_FULL", "TB_COACH", null);

        public static GuardianConsentDetails MotherConsent { get; } =
            new("Grace Akello", GuardianRelationship.Mother, "+256 772 410 233", [ConsentScope.DataProcessing], new DateOnly(2026, 11, 2));

        public static OfficialCapabilityDetails Referee { get; } = new("TESTBALL_FULL", "TB_REFEREE", "Class 1", "UG-REF-0147", new DateOnly(2027, 6, 30));
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

    /// <summary>Platform: an uploaded photo's row (its files aren't needed to prove isolation).</summary>
    private sealed class MediaAssetSample : IIsolationSample
    {
        public Type EntityType => typeof(MediaAsset);

        public Entity Add(DbContext context)
        {
            var id = MediaAsset.NewAssetId();
            var asset = MediaAsset.Create(
                id, MediaPurpose.PersonPhoto, "image/webp", $"isolation/{id:N}",
                [new MediaAssetFile(MediaVariant.Thumbnail, $"isolation/{id:N}/thumbnail.webp", "image/webp", 160, 120, 4_096)],
                SystemActor.Instance, RegistrySamples.Now);
            context.Add(asset);
            return asset;
        }

        public Entity? Referencing(IForeignKey foreignKey, Guid principalId) => null;
    }
}
