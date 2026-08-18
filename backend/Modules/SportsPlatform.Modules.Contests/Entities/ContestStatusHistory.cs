using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Contests.Entities;

public sealed class ContestStatusHistory : TenantEntity<Guid>
{
    public Guid ContestId { get; private set; }
    public CanonicalContestStatus FromStatus { get; private set; }
    public CanonicalContestStatus ToStatus { get; private set; }
    public string? PhaseCode { get; private set; }
    public Guid? ActorId { get; private set; }
    public string? Reason { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private ContestStatusHistory() { }

    public ContestStatusHistory(
        Guid id,
        Guid tenantId,
        Guid contestId,
        CanonicalContestStatus fromStatus,
        CanonicalContestStatus toStatus,
        DateTime occurredAt,
        Guid? actorId = null,
        string? reason = null,
        string? phaseCode = null)
        : base(id, tenantId)
    {
        ContestId = contestId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        OccurredAt = occurredAt;
        ActorId = actorId;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        PhaseCode = string.IsNullOrWhiteSpace(phaseCode) ? null : phaseCode.Trim();
        CreatedAt = occurredAt;
    }
}
