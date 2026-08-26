namespace BuildingBlocks.Kernel.Infrastructure.Outbox;

/// <summary>
/// A pending integration event persisted in the same transaction as the state
/// change that produced it (transactional outbox pattern).
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>Assembly-qualified-less full type name used to deserialize on publish.</summary>
    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTime OccurredOnUtc { get; set; }

    public DateTime? ProcessedOnUtc { get; set; }

    public string? Error { get; set; }
}