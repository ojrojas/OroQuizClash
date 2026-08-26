using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.EventBus.RabbitMQ.DependencyInjection;

public static class RabbitMqServiceCollectionExtensions
{
    /// <summary>
    /// Registers the RabbitMQ event bus. Binds <see cref="RabbitMqOptions"/> from
    /// the "EventBus:RabbitMq" configuration section.
    /// </summary>
    public static IEventBusBuilder AddRabbitMqEventBus(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<RabbitMqOptions>? configure = null)
    {
        var optionsBuilder = services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName));

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton<IRabbitMqConnection, RabbitMqConnection>();
        services.TryAddSingleton<IEventBus, RabbitMqEventBus>();
        services.TryAddSingleton<EventBusSubscriptionManager>();
        services.AddHostedService<RabbitMqConsumerService>();

        return new EventBusBuilder(services);
    }
}

public interface IEventBusBuilder
{
    /// <summary>Subscribes THandler to TEvent. The consumer binds the service queue to the event.</summary>
    IEventBusBuilder AddSubscription<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>;
}

internal sealed class EventBusBuilder(IServiceCollection services) : IEventBusBuilder
{
    public IEventBusBuilder AddSubscription<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<THandler>();

        services.AddOptions<SubscriptionRegistrations>().Configure(registrations =>
            registrations.Actions.Add(manager => manager.AddSubscription<TEvent, THandler>()));

        // Materialize registrations into the singleton manager on first resolve.
        services.RemoveAll<EventBusSubscriptionManager>();
        services.AddSingleton(provider =>
        {
            var manager = new EventBusSubscriptionManager();
            var registrations = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SubscriptionRegistrations>>();
            foreach (var register in registrations.Value.Actions)
            {
                register(manager);
            }

            return manager;
        });

        return this;
    }

    private sealed class SubscriptionRegistrations
    {
        public List<Action<EventBusSubscriptionManager>> Actions { get; } = [];
    }
}
