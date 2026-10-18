using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.PublishTenantEvents;

/// <summary><c>TenantStatusChanged.V1</c> in the Tenancy outbox, in the transaction of the status change.</summary>
public sealed class PublishTenantStatusChanged(TenancyDbContext context) : IDomainEventSubscriber<TenantStatusChangedDomainEvent>
{
    public Task HandleAsync(TenantStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        context.Publish(
            new TenantStatusChanged.V1(domainEvent.TenantId, domainEvent.From, domainEvent.To),
            PublishTenantCreated.AggregateType,
            domainEvent.TenantId);
        return Task.CompletedTask;
    }
}
