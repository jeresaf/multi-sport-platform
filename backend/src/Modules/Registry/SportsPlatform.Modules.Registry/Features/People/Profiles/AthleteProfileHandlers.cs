using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.People.Profiles;

/// <summary>
/// Adds an athlete profile (S06 D12): the variant enabled for the tenant and the positions the definition's
/// (<see cref="SportCodes"/>), refused on an archived or deceased person and when the person already holds an open
/// profile for the variant. The new row moves the person's version (D11), so a concurrent archive conflicts.
/// </summary>
public sealed class CreateAthleteProfileHandler(
    RegistryDbContext context, SportCodes sportCodes, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid personId, CreateAthleteProfileRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var variant = request.Variant!;
        audit.Describe(PeopleEndpoints.EntityType, personId, metadata: new AthleteProfileAuditMetadata(null, variant));

        var (person, refusal) = await PersonChanges.LoadAsync(
            context, personId, request.Version, ifMatch, cancellationToken, withAthleteProfiles: true);
        if (person is null)
        {
            return refusal;
        }

        var details = request.ToDetails();
        if (await sportCodes.CheckAthleteAsync(variant, isNew: true, details, cancellationToken) is { } unknown)
        {
            return unknown;
        }

        var added = person.AddAthleteProfile(variant, details, actor, timeProvider.GetUtcNow());
        if (added.IsFailure)
        {
            return new(added.Error, added.Error.Code == RegistryErrors.AthleteProfileExists.Code ? "variant" : null);
        }

        audit.Describe(metadata: new AthleteProfileAuditMetadata(added.Value.Id, variant));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}

/// <summary>
/// Replaces an athlete profile's details (S06 D12): a correction, so allowed whatever the person's status and whether
/// or not the variant is still enabled; the positions must be the variant's latest definition's.
/// </summary>
public sealed class UpdateAthleteProfileHandler(RegistryDbContext context, SportCodes sportCodes, AuditScope audit)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid personId, Guid profileId, UpdateAthleteProfileRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        audit.Describe(PeopleEndpoints.EntityType, personId);

        var (person, refusal) = await PersonChanges.LoadAsync(
            context, personId, request.Version, ifMatch, cancellationToken, withAthleteProfiles: true);
        if (person is null)
        {
            return refusal;
        }

        if (person.AthleteProfiles.SingleOrDefault(profile => profile.Id == profileId) is not { } profile)
        {
            return new(RegistryErrors.ProfileNotFound);
        }

        audit.Describe(metadata: new AthleteProfileAuditMetadata(profile.Id, profile.Variant));
        var details = request.ToDetails();
        if (ProfileTransitions.IsOpen(profile.Status)
            && await sportCodes.CheckAthleteAsync(profile.Variant, isNew: false, details, cancellationToken) is { } unknown)
        {
            return unknown;
        }

        var updated = person.UpdateAthleteProfile(profileId, details);
        if (updated.IsFailure)
        {
            return new(updated.Error);
        }

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}

/// <summary>Moves an athlete profile along its state machine with a reason (S06 D12; <see cref="ProfileTransitions"/>).</summary>
public sealed class ChangeAthleteProfileStatusHandler(
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
            context, personId, request.Version, ifMatch, cancellationToken, withAthleteProfiles: true);
        if (person is null)
        {
            return refusal;
        }

        if (person.AthleteProfiles.SingleOrDefault(profile => profile.Id == profileId) is not { } profile)
        {
            return new(RegistryErrors.ProfileNotFound);
        }

        audit.Describe(metadata: new AthleteProfileAuditMetadata(profile.Id, profile.Variant, profile.Status, status));
        var changed = person.ChangeAthleteProfileStatus(profileId, status, reason, actor, timeProvider.GetUtcNow());
        if (changed.IsFailure)
        {
            return new(changed.Error);
        }

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}

/// <summary>
/// The audit metadata of an athlete profile change: the profile by ID (none yet when a create is refused), its variant
/// and, for a status change, the move. Never a position or a public name.
/// </summary>
public sealed record AthleteProfileAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] Guid? ProfileId,
    [property: PersonalData(PersonalDataClass.Public)] string Variant,
    [property: PersonalData(PersonalDataClass.Internal)] ProfileStatus? FromStatus = null,
    [property: PersonalData(PersonalDataClass.Internal)] ProfileStatus? ToStatus = null);
