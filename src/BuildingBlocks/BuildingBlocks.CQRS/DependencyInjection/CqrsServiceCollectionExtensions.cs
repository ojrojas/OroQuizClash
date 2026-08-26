namespace BuildingBlocks.CQRS.DependencyInjection;

public sealed class CqrsConfiguration
{
    internal List<Assembly> Assemblies { get; } = [];

    internal List<Type> OpenBehaviors { get; } = [];

    public CqrsConfiguration RegisterHandlersFromAssembly(Assembly assembly)
    {
        Assemblies.Add(assembly);
        return this;
    }

    public CqrsConfiguration RegisterHandlersFromAssemblyContaining<TMarker>() =>
        RegisterHandlersFromAssembly(typeof(TMarker).Assembly);

    /// <summary>Adds an open-generic pipeline behavior, e.g. typeof(LoggingBehavior&lt;,&gt;). Order matters.</summary>
    public CqrsConfiguration AddOpenBehavior(Type openBehaviorType)
    {
        OpenBehaviors.Add(openBehaviorType);
        return this;
    }
}

public static class CqrsServiceCollectionExtensions
{
    private static readonly Type[] HandlerInterfaces =
    [
        typeof(IRequestHandler<,>),
        typeof(IDomainEventHandler<>),
        typeof(IValidator<>)
    ];

    /// <summary>
    /// Registers the sender, the domain event dispatcher, the configured pipeline
    /// behaviors, and every handler/validator found in the given assemblies.
    /// </summary>
    public static IServiceCollection AddCqrs(this IServiceCollection services, Action<CqrsConfiguration> configure)
    {
        var configuration = new CqrsConfiguration();
        configure(configuration);

        services.TryAddScoped<ISender, Sender>();
        services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        foreach (var openBehavior in configuration.OpenBehaviors)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), openBehavior);
        }

        foreach (var assembly in configuration.Assemblies.Distinct())
        {
            RegisterImplementations(services, assembly);
        }

        return services;
    }

    private static void RegisterImplementations(IServiceCollection services, Assembly assembly)
    {
        var implementations =
            from type in assembly.DefinedTypes
            where type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
            from @interface in type.ImplementedInterfaces
            where @interface.IsGenericType && HandlerInterfaces.Contains(@interface.GetGenericTypeDefinition())
            select (Service: @interface, Implementation: (Type)type);

        foreach (var (service, implementation) in implementations)
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped(service, implementation));

            // Handlers are also resolvable through the base IRequestHandler<,> the Sender asks for.
            if (service.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            {
                services.TryAdd(ServiceDescriptor.Scoped(service, implementation));
            }
        }
    }
}