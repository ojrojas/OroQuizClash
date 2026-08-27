using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Games.Events;

public sealed record ScoreUpdatedDomainEvent(
    Guid GameId,
    Guid PlayerId,
    int Points,
    int ResultingBalance,
    string Type) : DomainEvent;
