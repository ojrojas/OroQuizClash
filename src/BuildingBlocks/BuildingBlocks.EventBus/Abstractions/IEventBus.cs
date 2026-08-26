namespace BuildingBlocks.EventBus.Abstractions;

/// <summary>
/// Publishes integration events to the configured transport (e.g. RabbitMQ).
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent;
}
