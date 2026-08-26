namespace BuildingBlocks.EventBus.Abstractions;

/// <summary>
/// A fact published to other services/bounded contexts through the event bus.
/// Unlike domain events, integration events cross process boundaries and must be
/// serializable and versioned with care.
/// </summary>
public abstract record IntegrationEvent
{
    protected IntegrationEvent()
    {
        Id = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
    }

    [JsonConstructor]
    protected IntegrationEvent(Guid id, DateTime occurredOnUtc)
    {
        Id = id;
        OccurredOnUtc = occurredOnUtc;
    }

    [JsonInclude]
    public Guid Id { get; private init; }

    [JsonInclude]
    public DateTime OccurredOnUtc { get; private init; }
}
