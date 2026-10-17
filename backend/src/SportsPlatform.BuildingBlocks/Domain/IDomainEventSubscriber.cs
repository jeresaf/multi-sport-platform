namespace SportsPlatform.BuildingBlocks.Domain;

/// <summary>
/// Handles a domain event in the raising module, during <c>SaveChangesAsync</c> and before the transaction commits.
/// Changes it makes (and integration events it publishes) are saved in the same transaction; events it raises are
/// dispatched in the next round.
/// </summary>
public interface IDomainEventSubscriber<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
