namespace BuildingBlocks.EventBus.RabbitMQ;

public sealed class RabbitMqOptions
{
    public const string SectionName = "EventBus:RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string VirtualHost { get; set; } = "/";

    /// <summary>Topic exchange all integration events are published to.</summary>
    public string ExchangeName { get; set; } = "integration_events";

    /// <summary>
    /// Queue for this service's subscriptions. Use one queue per service so
    /// every service gets its own copy of each event. Required when subscribing.
    /// </summary>
    public string QueueName { get; set; } = string.Empty;

    /// <summary>Retries when publishing or connecting fails.</summary>
    public int RetryCount { get; set; } = 5;

    /// <summary>Max messages delivered to this consumer without ack.</summary>
    public ushort PrefetchCount { get; set; } = 10;
}
