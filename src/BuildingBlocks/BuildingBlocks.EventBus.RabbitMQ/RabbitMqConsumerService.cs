namespace BuildingBlocks.EventBus.RabbitMQ;

/// <summary>
/// Hosted service that declares the service queue, binds it to every subscribed
/// event name, and dispatches incoming messages to the registered handlers.
/// Messages are acked only after all handlers succeed; failures are nacked
/// without requeue (pair the queue with a dead-letter exchange in production).
/// </summary>
public sealed class RabbitMqConsumerService(
    IRabbitMqConnection connection,
    EventBusSubscriptionManager subscriptionManager,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConsumerService> logger) : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (subscriptionManager.Subscriptions.Count == 0)
        {
            logger.LogInformation("No integration event subscriptions registered; RabbitMQ consumer will not start");
            return;
        }

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.QueueName))
        {
            throw new InvalidOperationException(
                $"'{RabbitMqOptions.SectionName}:{nameof(RabbitMqOptions.QueueName)}' must be configured to consume integration events.");
        }

        var amqpConnection = await connection.GetConnectionAsync(stoppingToken).ConfigureAwait(false);
        _channel = await amqpConnection.CreateChannelAsync(cancellationToken: stoppingToken).ConfigureAwait(false);

        await _channel.ExchangeDeclareAsync(settings.ExchangeName, ExchangeType.Topic, durable: true,
            autoDelete: false, cancellationToken: stoppingToken).ConfigureAwait(false);

        await _channel.QueueDeclareAsync(settings.QueueName, durable: true, exclusive: false,
            autoDelete: false, cancellationToken: stoppingToken).ConfigureAwait(false);

        foreach (var subscription in subscriptionManager.Subscriptions)
        {
            await _channel.QueueBindAsync(settings.QueueName, settings.ExchangeName,
                routingKey: subscription.EventName, cancellationToken: stoppingToken).ConfigureAwait(false);

            logger.LogInformation("Subscribed queue {QueueName} to {EventName}", settings.QueueName, subscription.EventName);
        }

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: settings.PrefetchCount,
            global: false, cancellationToken: stoppingToken).ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, eventArgs) => OnMessageReceivedAsync(eventArgs, stoppingToken);

        await _channel.BasicConsumeAsync(settings.QueueName, autoAck: false, consumer,
            cancellationToken: stoppingToken).ConfigureAwait(false);

        // Keep the service alive until shutdown; the consumer callback does the work.
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
    }

    private async Task OnMessageReceivedAsync(BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {
        var eventName = eventArgs.BasicProperties.Type ?? eventArgs.RoutingKey;

        try
        {
            await ProcessEventAsync(eventName, eventArgs.Body, cancellationToken).ConfigureAwait(false);
            await _channel!.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error processing integration event {EventName}", eventName);
            await _channel!.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessEventAsync(string eventName, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        if (!subscriptionManager.TryGetSubscription(eventName, out var subscription))
        {
            logger.LogWarning("Received integration event {EventName} with no subscription; ignoring", eventName);
            return;
        }

        var integrationEvent = (IntegrationEvent?)JsonSerializer.Deserialize(
            body.Span, subscription.EventType, RabbitMqEventBus.SerializerOptions)
            ?? throw new InvalidOperationException($"Could not deserialize integration event '{eventName}'.");

        await using var scope = scopeFactory.CreateAsyncScope();

        foreach (var handlerType in subscription.HandlerTypes)
        {
            var handler = scope.ServiceProvider.GetRequiredService(handlerType);

            var handleMethod = handlerType.GetMethod(nameof(IIntegrationEventHandler<IntegrationEvent>.HandleAsync),
                [subscription.EventType, typeof(CancellationToken)])!;

            await ((Task)handleMethod.Invoke(handler, [integrationEvent, cancellationToken])!).ConfigureAwait(false);
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        base.Dispose();
    }
}
