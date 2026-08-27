using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Games.Events;

public sealed record PlayerEliminatedDomainEvent(
    Guid GameId,
    Guid PlayerId,
    string Reason) : DomainEvent;
