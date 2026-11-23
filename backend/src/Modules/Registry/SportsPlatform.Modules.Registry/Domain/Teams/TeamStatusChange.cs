using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.Teams;

/// <summary>
/// One row of a team's status history (<c>registry.team_status_changes</c>, append-only, tenant-owned, S07 D12).
/// <see cref="FromStatus"/> is null only on the row written when the team is created. The reason is free text kept
/// permanently: never personal data.
/// </summary>
public sealed class TeamStatusChange : Entity, ITenantOwned
{
    public const int ReasonMaxLength = 500;

    /// <summary>A row of a team's history; the team writes one with every status change.</summary>
    public TeamStatusChange(
        Guid teamId,
        TeamStatus? fromStatus,
        TeamStatus toStatus,
        string? reason,
        ICurrentActor actor,
        DateTimeOffset occurredAt)
        : base(NewId())
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("A status change belongs to a team.", nameof(teamId));
        }

        if (reason is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, ReasonMaxLength, nameof(reason));
        }

        TeamId = teamId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
        ActorKind = actor.Kind;
        ActorId = actor.ActorId;
        OccurredAt = occurredAt;
    }

    private TeamStatusChange()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid TeamId { get; private set; }

    public TeamStatus? FromStatus { get; private set; }

    public TeamStatus ToStatus { get; private set; }

    public string? Reason { get; private set; }

    public ActorKind ActorKind { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
