using BuildingBlocks.Kernel.Domain.Events;
namespace OroQuizClash.Domain.Games.Events;
public sealed record GameForcedFinishedDomainEvent(Guid GameId, string Reason) : DomainEvent;
