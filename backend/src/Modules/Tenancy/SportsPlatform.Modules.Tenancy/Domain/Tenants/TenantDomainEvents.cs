using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Domain.Tenants;

public sealed record TenantCreatedDomainEvent(Guid TenantId, string Slug, TenantStatus Status) : IDomainEvent;

public sealed record TenantStatusChangedDomainEvent(Guid TenantId, TenantStatus From, TenantStatus To) : IDomainEvent;
