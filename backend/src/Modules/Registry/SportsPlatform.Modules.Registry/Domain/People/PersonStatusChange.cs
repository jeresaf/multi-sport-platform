using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.People;

/// <summary>
/// One row of a person's status history (<c>registry.person_status_changes</c>, append-only, tenant-owned, S06 D9).
/// <see cref="FromStatus"/> is null only on the row written when the person is created. The reason is free text kept
/// permanently: never personal data.
/// </summary>
public sealed class PersonStatusChange : Entity, ITenantOwned
{
    public const int ReasonMaxLength = 500;

    /// <summary>A row of a person's history; the person writes one with every status change.</summary>
    public PersonStatusChange(
        Guid personId, PersonStatus? fromStatus, PersonStatus toStatus, string? reason, ICurrentActor actor, DateTimeOffset occurredAt)
        : base(NewId())
    {
        ArgumentNullException.ThrowIfNull(actor);
        PersonId = personId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
        ActorKind = actor.Kind;
        ActorId = actor.ActorId;
        OccurredAt = occurredAt;
    }

    private PersonStatusChange()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid PersonId { get; private set; }

    public PersonStatus? FromStatus { get; private set; }

    public PersonStatus ToStatus { get; private set; }

    public string? Reason { get; private set; }

    public ActorKind ActorKind { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
