using SportsPlatform.BuildingBlocks.Messaging;

namespace SportsPlatform.Modules.Tenancy.Contracts.Tenants;

/// <summary>
/// A tenant's status changed along the tenant state machine. IDs and statuses only (ADR-0015, S02 D10): the reason stays
/// in Tenancy's status history and the audit trail.
/// </summary>
public static class TenantStatusChanged
{
    public sealed record V1(Guid TenantId, TenantStatus From, TenantStatus To) : IIntegrationEvent;
}
