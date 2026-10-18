using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Domain.Tenants;

/// <summary>
/// One row of a tenant's status history (append-only). <see cref="FromStatus"/> is null only on the row written when
/// the tenant is created. Tenant-owned (RLS): written and read under the tenant it belongs to. The reason is free text
/// kept permanently: never personal data.
/// </summary>
public sealed class TenantStatusChange : Entity, ITenantOwned
{
    public const int ReasonMaxLength = 500;

    internal TenantStatusChange(
        Guid tenantId, TenantStatus? fromStatus, TenantStatus toStatus, string? reason, ICurrentActor actor, DateTimeOffset occurredAt)
        : base(NewId())
    {
        TenantId = tenantId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
        ActorKind = actor.Kind;
        ActorId = actor.ActorId;
        OccurredAt = occurredAt;
    }

    private TenantStatusChange()
    {
    }

    public Guid TenantId { get; private set; }

    public TenantStatus? FromStatus { get; private set; }

    public TenantStatus ToStatus { get; private set; }

    public string? Reason { get; private set; }

    public ActorKind ActorKind { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
