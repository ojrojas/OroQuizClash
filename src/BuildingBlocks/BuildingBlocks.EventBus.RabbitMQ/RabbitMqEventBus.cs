namespace BuildingBlocks.EventBus.RabbitMQ;

/// <summary>
/// Publishes integration events to a durable topic exchange with publisher
/// confirmations, using the event name as routing key.
/// </summary>
public sealed class RabbitMqEventBus(
    IRabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqEventBus> logger) : IEventBus
{
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        var settings = options.Value;
        var eventName = EventBusSubscriptionManager.GetEventName(integrationEvent.GetType());
        var body = JsonSerializer.SerializeToUtf8Bytes(integrationEvent, integrationEvent.GetType(), SerializerOptions);

        var amqpConnection = await connection.GetConnectionAsync(cancellationToken).ConfigureAwait(false);

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        await using var channel = await amqpConnection.CreateChannelAsync(channelOptions, cancellationToken).ConfigureAwait(false);

        await channel.ExchangeDeclareAsync(settings.ExchangeName, ExchangeType.Topic, durable: true,
            autoDelete: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        var properties = new BasicProperties
        {
            MessageId = integrationEvent.Id.ToString(),
            Type = eventName,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        var attempt = 0;
        while (true)
        {
            try
            {
                await channel.BasicPublishAsync(
                    exchange: settings.ExchangeName,
                    routingKey: eventName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                logger.LogInformation("Published integration event {EventName} ({EventId})", eventName, integrationEvent.Id);
                return;
            }
            catch (Exception exception) when (attempt < settings.RetryCount)
            {
                attempt++;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                logger.LogWarning(exception,
                    "Failed to publish {EventName} (attempt {Attempt}/{RetryCount}). Retrying in {Delay}s",
                    eventName, attempt, settings.RetryCount, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}