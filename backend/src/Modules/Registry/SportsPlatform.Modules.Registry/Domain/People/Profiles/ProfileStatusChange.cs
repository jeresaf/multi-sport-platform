using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.People.Profiles;

/// <summary>
/// One row of a role profile's status history (<c>registry.profile_status_changes</c>, append-only, tenant-owned, S06
/// D12): one table for every profile kind, pointing at the person (the profile by kind and ID). <see cref="FromStatus"/>
/// is null only on the row written when the profile is created. The reason is free text kept permanently: never
/// personal data.
/// </summary>
public sealed class ProfileStatusChange : Entity, ITenantOwned
{
    public const int ReasonMaxLength = 500;

    /// <summary>A row of a profile's history; the person writes one with every profile status change.</summary>
    public ProfileStatusChange(
        Guid personId, ProfileKind profileKind, Guid profileId, ProfileStatus? fromStatus, ProfileStatus toStatus, string? reason,
        ICurrentActor actor, DateTimeOffset occurredAt)
        : base(NewId())
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (personId == Guid.Empty || profileId == Guid.Empty)
        {
            throw new ArgumentException("A profile status change belongs to a person's profile.", nameof(profileId));
        }

        if (reason is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, ReasonMaxLength, nameof(reason));
        }

        PersonId = personId;
        ProfileKind = profileKind;
        ProfileId = profileId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
        ActorKind = actor.Kind;
        ActorId = actor.ActorId;
        OccurredAt = occurredAt;
    }

    private ProfileStatusChange()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid PersonId { get; private set; }

    public ProfileKind ProfileKind { get; private set; }

    public Guid ProfileId { get; private set; }

    public ProfileStatus? FromStatus { get; private set; }

    public ProfileStatus ToStatus { get; private set; }

    public string? Reason { get; private set; }

    public ActorKind ActorKind { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
