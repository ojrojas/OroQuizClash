using BuildingBlocks.Kernel.Domain.Events;
namespace OroQuizClash.Domain.Games.Events;
public sealed record GameCancelledDomainEvent(Guid GameId, string Reason) : DomainEvent;
