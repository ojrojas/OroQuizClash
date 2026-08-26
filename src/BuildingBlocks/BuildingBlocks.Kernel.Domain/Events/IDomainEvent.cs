namespace BuildingBlocks.Kernel.Domain.Events;

/// <summary>
/// Something meaningful that happened inside the domain.
/// Domain events are raised by aggregates and dispatched in-process
/// (typically just before/after persisting the aggregate).
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }
}
