using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Games.Events;

public sealed record PlayerWithdrawnDomainEvent(
    Guid GameId,
    Guid PlayerId,
    int RetainedPoints,
    string PolicyName) : DomainEvent;
