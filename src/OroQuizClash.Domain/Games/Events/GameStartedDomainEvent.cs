using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Games.Events;

public sealed record GameStartedDomainEvent(Guid GameId) : DomainEvent;