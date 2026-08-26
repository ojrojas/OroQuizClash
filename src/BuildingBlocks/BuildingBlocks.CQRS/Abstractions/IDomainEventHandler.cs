

namespace BuildingBlocks.CQRS.Abstractions;

/// <summary>
/// Reacts in-process to a domain event raised by an aggregate.
/// Multiple handlers per event are allowed.
/// </summary>
public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    Task HandleAsync(TDomainEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Dispatches domain events to all registered <see cref="IDomainEventHandler{TDomainEvent}"/>.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
