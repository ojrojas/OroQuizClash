namespace BuildingBlocks.CQRS.Dispatching;

public sealed class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, DomainEventHandlerWrapper> Wrappers = new();

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            logger.LogDebug("Dispatching domain event {DomainEvent}", domainEvent.GetType().Name);

            var wrapper = Wrappers.GetOrAdd(domainEvent.GetType(), static eventType =>
            {
                var wrapperType = typeof(DomainEventHandlerWrapperImpl<>).MakeGenericType(eventType);
                return (DomainEventHandlerWrapper)Activator.CreateInstance(wrapperType)!;
            });

            await wrapper.HandleAsync(domainEvent, serviceProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private abstract class DomainEventHandlerWrapper
    {
        public abstract Task HandleAsync(
            IDomainEvent domainEvent,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken);
    }

    private sealed class DomainEventHandlerWrapperImpl<TDomainEvent> : DomainEventHandlerWrapper
        where TDomainEvent : IDomainEvent
    {
        public override async Task HandleAsync(
            IDomainEvent domainEvent,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<TDomainEvent>>())
            {
                await handler.HandleAsync((TDomainEvent)domainEvent, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}