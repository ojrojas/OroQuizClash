namespace BuildingBlocks.EventBus.Subscriptions;

/// <summary>
/// In-memory registry mapping event names to event/handler types.
/// Populated at startup; read by the transport consumer to deserialize and dispatch.
/// </summary>
public sealed class EventBusSubscriptionManager
{
    private readonly Dictionary<string, SubscriptionInfo> _subscriptions = [];

    public IReadOnlyCollection<SubscriptionInfo> Subscriptions => _subscriptions.Values;

    public static string GetEventName<TEvent>() where TEvent : IntegrationEvent => GetEventName(typeof(TEvent));

    public static string GetEventName(Type eventType) => eventType.Name;

    public void AddSubscription<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IIntegrationEventHandler<TEvent>
    {
        var eventName = GetEventName<TEvent>();

        if (_subscriptions.TryGetValue(eventName, out var existing))
        {
            existing.HandlerTypes.Add(typeof(THandler));
            return;
        }

        _subscriptions[eventName] = new SubscriptionInfo(eventName, typeof(TEvent), [typeof(THandler)]);
    }

    public bool TryGetSubscription(string eventName, out SubscriptionInfo subscription) =>
        _subscriptions.TryGetValue(eventName, out subscription!);
}

public sealed record SubscriptionInfo(string EventName, Type EventType, List<Type> HandlerTypes);