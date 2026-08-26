using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Infrastructure.Outbox;
using BuildingBlocks.Kernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Kernel.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Exposes an already-registered <typeparamref name="TDbContext"/> as the
    /// <see cref="IUnitOfWork"/> of the request scope.
    /// </summary>
    public static IServiceCollection AddUnitOfWork<TDbContext>(this IServiceCollection services)
        where TDbContext : AppDbContextBase
    {
        services.TryAddScoped<IUnitOfWork>(provider => provider.GetRequiredService<TDbContext>());
        return services;
    }

    /// <summary>
    /// Registers the transactional outbox for <typeparamref name="TDbContext"/>:
    /// the writer used by handlers and the background processor that publishes
    /// pending messages to the event bus. The DbContext model must include
    /// <see cref="OutboxMessage"/> (apply <see cref="OutboxEntityTypeConfiguration"/>).
    /// </summary>
    public static IServiceCollection AddOutbox<TDbContext>(
        this IServiceCollection services,
        Action<OutboxOptions>? configure = null)
        where TDbContext : DbContext
    {
        var optionsBuilder = services.AddOptions<OutboxOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.TryAddScoped<IOutboxWriter, OutboxWriter<TDbContext>>();
        services.AddHostedService<OutboxProcessor<TDbContext>>();

        return services;
    }
}
