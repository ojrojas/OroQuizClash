using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Games.Events;

public sealed record GameCreatedDomainEvent(Guid GameId) : DomainEvent;