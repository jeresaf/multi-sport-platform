using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Registry.Infrastructure.Configurations;

namespace SportsPlatform.Modules.Registry.Features.People.Profiles;

/// <summary>
/// Adds an official profile (S06 D12): every capability's variant enabled for the tenant and its role the definition's
/// (<see cref="SportCodes"/>), refused on an archived or deceased person and when the person already holds an open official
/// profile. The new rows move the person's version (D11), so a concurrent archive conflicts; a concurrent create is
/// refused by the version or by the open-profile index.
/// </summary>
public sealed class CreateOfficialProfileHandler(
    RegistryDbContext context, SportCodes sportCodes, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid personId, OfficialProfileRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var capabilities = request.ToDetails();
        audit.Describe(PeopleEndpoints.EntityType, personId, metadata: OfficialProfileAuditMetadata.Of(null, capabilities));

        var (person, refusal) = await PersonChanges.LoadAsync(
            context, personId, request.Version, ifMatch, cancellationToken, withOfficialProfiles: true);
        if (person is null)
        {
            return refusal;
        }

        if (await sportCodes.CheckOfficialAsync(capabilities, existing: null, cancellationToken) is { } unknown)
        {
            return unknown;
        }

        var added = person.AddOfficialProfile(capabilities, actor, timeProvider.GetUtcNow());
        if (added.IsFailure)
        {
            return new(added.Error);
        }

        audit.Describe(metadata: OfficialProfileAuditMetadata.Of(added.Value.Id, capabilities));
        try
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            return saved.IsFailure ? new(saved.Error) : null;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: OfficialProfileConfiguration.OpenProfileIndex,
        })
        {
            // Two creates for one person at once (as review #101 R1): the second's insert can reach the open-profile
            // index before the person's version is checked.
            return new(RegistryErrors.OfficialProfileExists);
        }
    }
}

/// <summary>
/// Replaces an official profile's capabilities (S06 D12): allowed whatever the person's status. A capability the profile
/// already holds may stay on a variant the tenant has since disabled; an added one needs its variant enabled. Every role
/// must be the variant's latest definition's.
/// </summary>
public sealed class UpdateOfficialProfileHandler(RegistryDbContext context, SportCodes sportCodes, AuditScope audit)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid personId, Guid profileId, OfficialProfileRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        audit.Describe(PeopleEndpoints.EntityType, personId);

        var (person, refusal) = await PersonChanges.LoadAsync(
            context, personId, request.Version, ifMatch, cancellationToken, withOfficialProfiles: true);
        if (person is null)
        {
            return refusal;
        }

        if (person.OfficialProfiles.SingleOrDefault(profile => profile.Id == profileId) is not { } profile)
        {
            return new(RegistryErrors.ProfileNotFound);
        }

        var capabilities = request.ToDetails();
        audit.Describe(metadata: OfficialProfileAuditMetadata.Of(profile.Id, capabilities, profile));
        if (ProfileTransitions.IsOpen(profile.Status)
            && await sportCodes.CheckOfficialAsync(capabilities, profile, cancellationToken) is { } unknown)
        {
            return unknown;
        }

        var updated = person.UpdateOfficialProfile(profileId, capabilities);
        if (updated.IsFailure)
        {
            return new(updated.Error);
        }

        try
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            return saved.IsFailure ? new(saved.Error) : null;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: OfficialCapabilityConfiguration.RoleIndex,
        })
        {
            // Two updates adding the same capability at once: the second's insert can reach the index before the
            // person's version is checked. The person changed under it, as a version mismatch would say.
            return new(RegistryErrors.ConcurrencyConflict);
        }
    }
}

/// <summary>Moves an official profile along its state machine with a reason (S06 D12; <see cref="ProfileTransitions"/>).</summary>
public sealed class ChangeOfficialProfileStatusHandler(
    RegistryDbContext context, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid personId, Guid profileId, ChangeProfileStatusRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = request.Reason!.Trim();
        var status = request.Status!.Value;
        audit.Describe(PeopleEndpoints.EntityType, personId, reason);

        var (person, refusal) = await PersonChanges.LoadAsync(
            context, personId, request.Version, ifMatch, cancellationToken, withOfficialProfiles: true);
        if (person is null)
        {
            return refusal;
        }

        if (person.OfficialProfiles.SingleOrDefault(profile => profile.Id == profileId) is not { } profile)
        {
            return new(RegistryErrors.ProfileNotFound);
        }

        audit.Describe(metadata: new OfficialProfileAuditMetadata(profile.Id, Variants(profile), profile.Status, status));
        var changed = person.ChangeOfficialProfileStatus(profileId, status, reason, actor, timeProvider.GetUtcNow());
        if (changed.IsFailure)
        {
            return new(changed.Error);
        }

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }

    private static IReadOnlyList<string> Variants(OfficialProfile profile) =>
        [.. profile.Capabilities.Select(c => c.Variant).Distinct().Order(StringComparer.Ordinal)];
}

/// <summary>
/// The audit metadata of an official profile change: the profile by ID (none yet when a create is refused), the variants of
/// its capabilities (as sent, for a create or an update), the capabilities added and removed by variant and role (owner,
/// review #102 R1: a removed capability is deleted, so this is its only trace) and, for a status change, the move. Never
/// a grade or a licence.
/// </summary>
public sealed record OfficialProfileAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] Guid? ProfileId,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<string> Variants,
    [property: PersonalData(PersonalDataClass.Internal)] ProfileStatus? FromStatus = null,
    [property: PersonalData(PersonalDataClass.Internal)] ProfileStatus? ToStatus = null,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<CapabilityAuditEntry>? Added = null,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<CapabilityAuditEntry>? Removed = null)
{
    /// <summary>
    /// The metadata of a create (<paramref name="existing"/> null: every capability is added) or of an update of
    /// <paramref name="existing"/> to <paramref name="capabilities"/>.
    /// </summary>
    public static OfficialProfileAuditMetadata Of(
        Guid? profileId, IReadOnlyList<OfficialCapabilityDetails> capabilities, OfficialProfile? existing = null) =>
        new(
            profileId,
            [.. capabilities.Select(c => c.Variant).Distinct().Order(StringComparer.Ordinal)],
            Added: CapabilityAuditEntry.Sorted(capabilities.Where(c => existing is null || !existing.Holds(c.Variant, c.Role)).Select(c => (c.Variant, c.Role))),
            Removed: existing is null
                ? []
                : CapabilityAuditEntry.Sorted(existing.Capabilities
                    .Where(held => !capabilities.Any(c => c.Variant == held.Variant && c.Role == held.Role))
                    .Select(held => (held.Variant, held.Role))));
}
