using SportsPlatform.BuildingBlocks.Messaging;

namespace SportsPlatform.Modules.Tenancy.Contracts.Tenants;

/// <summary>A tenant was created (status <c>PROVISIONING</c>). IDs and <c>PUBLIC</c> fields only (ADR-0015).</summary>
public static class TenantCreated
{
    public sealed record V1(Guid TenantId, string Slug, TenantStatus Status) : IIntegrationEvent;
}
