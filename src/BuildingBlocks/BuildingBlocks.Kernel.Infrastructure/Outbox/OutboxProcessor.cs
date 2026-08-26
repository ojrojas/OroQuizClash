using System.Collections.Concurrent;
using System.Text.Json;

using BuildingBlocks.EventBus.Abstractions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Kernel.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 50;
}

/// <summary>
/// Polls the outbox table and publishes pending messages to the event bus,
/// marking each as processed (or recording the error) afterwards.
/// </summary>
public sealed class OutboxProcessor<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor<TDbContext>> logger) : BackgroundService
    where TDbContext : DbContext
{
    private static readonly ConcurrentDictionary<string, Type?> EventTypeCache = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox processing cycle failed");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var messages = await dbContext.Set<OutboxMessage>()
            .Where(message => message.ProcessedOnUtc == null)
            .OrderBy(message => message.OccurredOnUtc)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var message in messages)
        {
            try
            {
                var eventType = ResolveEventType(message.EventType)
                    ?? throw new InvalidOperationException($"Integration event type '{message.EventType}' not found.");

                var integrationEvent = (IntegrationEvent?)JsonSerializer.Deserialize(message.Payload, eventType, OutboxSerialization.Options)
                    ?? throw new InvalidOperationException($"Could not deserialize outbox message {message.Id}.");

                await eventBus.PublishAsync(integrationEvent, cancellationToken).ConfigureAwait(false);

                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to publish outbox message {MessageId}", message.Id);
                message.Error = exception.Message;
            }
        }

        if (messages.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static Type? ResolveEventType(string typeName) =>
        EventTypeCache.GetOrAdd(typeName, static name =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(name))
                .FirstOrDefault(type => type is not null));
}