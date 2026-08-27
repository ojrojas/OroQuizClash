using BuildingBlocks.Kernel.Domain.Events;
namespace OroQuizClash.Domain.Games.Events;
public sealed record GameFinishedDomainEvent(Guid GameId) : DomainEvent;
