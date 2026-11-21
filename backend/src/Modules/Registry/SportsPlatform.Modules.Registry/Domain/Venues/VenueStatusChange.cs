using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.Venues;

/// <summary>
/// One row of a venue's status history (<c>registry.venue_status_changes</c>, append-only, tenant-owned, S07 D12).
/// <see cref="FromStatus"/> is null only on the row written when the venue is created. The reason is free text kept
/// permanently: never personal data.
/// </summary>
public sealed class VenueStatusChange : Entity, ITenantOwned
{
    public const int ReasonMaxLength = 500;

    /// <summary>A row of a venue's history; the venue writes one with every status change.</summary>
    public VenueStatusChange(
        Guid venueId, VenueStatus? fromStatus, VenueStatus toStatus, string? reason, ICurrentActor actor, DateTimeOffset occurredAt)
        : base(NewId())
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (venueId == Guid.Empty)
        {
            throw new ArgumentException("A status change belongs to a venue.", nameof(venueId));
        }

        if (reason is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, ReasonMaxLength, nameof(reason));
        }

        VenueId = venueId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
        ActorKind = actor.Kind;
        ActorId = actor.ActorId;
        OccurredAt = occurredAt;
    }

    private VenueStatusChange()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid VenueId { get; private set; }

    public VenueStatus? FromStatus { get; private set; }

    public VenueStatus ToStatus { get; private set; }

    public string? Reason { get; private set; }

    public ActorKind ActorKind { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
