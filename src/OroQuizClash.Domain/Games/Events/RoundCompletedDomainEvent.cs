using BuildingBlocks.Kernel.Domain.Events;
namespace OroQuizClash.Domain.Games.Events;
public sealed record RoundCompletedDomainEvent(Guid GameId, Guid RoundId) : DomainEvent;
