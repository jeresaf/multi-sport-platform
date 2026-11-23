using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.Organisations;

/// <summary>
/// One row of an organisation's status history (<c>registry.organisation_status_changes</c>, append-only, tenant-owned,
/// S07 D12). <see cref="FromStatus"/> is null only on the row written when the organisation is created. The reason is
/// free text kept permanently: never personal data.
/// </summary>
public sealed class OrganisationStatusChange : Entity, ITenantOwned
{
    public const int ReasonMaxLength = 500;

    /// <summary>A row of an organisation's history; the organisation writes one with every status change.</summary>
    public OrganisationStatusChange(
        Guid organisationId,
        OrganisationStatus? fromStatus,
        OrganisationStatus toStatus,
        string? reason,
        ICurrentActor actor,
        DateTimeOffset occurredAt)
        : base(NewId())
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException("A status change belongs to an organisation.", nameof(organisationId));
        }

        if (reason is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, ReasonMaxLength, nameof(reason));
        }

        OrganisationId = organisationId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
        ActorKind = actor.Kind;
        ActorId = actor.ActorId;
        OccurredAt = occurredAt;
    }

    private OrganisationStatusChange()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid OrganisationId { get; private set; }

    public OrganisationStatus? FromStatus { get; private set; }

    public OrganisationStatus ToStatus { get; private set; }

    public string? Reason { get; private set; }

    public ActorKind ActorKind { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
