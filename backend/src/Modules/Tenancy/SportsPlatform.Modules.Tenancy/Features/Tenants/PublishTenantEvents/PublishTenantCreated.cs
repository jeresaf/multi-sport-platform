using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.PublishTenantEvents;

/// <summary><c>TenantCreated.V1</c> in the Tenancy outbox, in the transaction that creates the tenant.</summary>
public sealed class PublishTenantCreated(TenancyDbContext context) : IDomainEventSubscriber<TenantCreatedDomainEvent>
{
    public const string AggregateType = "Tenant";

    public Task HandleAsync(TenantCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        context.Publish(new TenantCreated.V1(domainEvent.TenantId, domainEvent.Slug, domainEvent.Status), AggregateType, domainEvent.TenantId);
        return Task.CompletedTask;
    }
}
