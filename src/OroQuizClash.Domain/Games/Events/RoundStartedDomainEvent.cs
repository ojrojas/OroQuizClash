using BuildingBlocks.Kernel.Domain.Events;
namespace OroQuizClash.Domain.Games.Events;
public sealed record RoundStartedDomainEvent(Guid GameId, Guid RoundId, int RoundNumber, Guid QuestionId) : DomainEvent;
