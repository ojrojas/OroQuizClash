using System.Text.Json;
using BuildingBlocks.EventBus.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Kernel.Infrastructure.Outbox;

public interface IOutboxWriter
{
    /// <summary>
    /// Stages an integration event in the outbox. Call before SaveChanges so the
    /// event is committed atomically with the business change.
    /// </summary>
    Task StageAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent;
}

public sealed class OutboxWriter<TDbContext>(TDbContext dbContext) : IOutboxWriter
    where TDbContext : DbContext
{
    public async Task StageAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        var message = new OutboxMessage
        {
            Id = integrationEvent.Id,
            EventType = integrationEvent.GetType().FullName!,
            Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), OutboxSerialization.Options),
            OccurredOnUtc = integrationEvent.OccurredOnUtc
        };

        await dbContext.Set<OutboxMessage>().AddAsync(message, cancellationToken).ConfigureAwait(false);
    }
}

internal static class OutboxSerialization
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
