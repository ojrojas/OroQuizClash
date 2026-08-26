namespace BuildingBlocks.EventBus.Abstractions;

/// <summary>
/// Handles an integration event received from the bus.
/// Handlers must be idempotent: the transport guarantees at-least-once delivery.
/// </summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
