using BuildingBlocks.Kernel.Domain.Events;
namespace OroQuizClash.Domain.Games.Events;
public sealed record PlayerJoinedDomainEvent(Guid GameId, Guid UserId) : DomainEvent;
