using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Games.Events;

public sealed record AnswerSubmittedDomainEvent(
    Guid GameId,
    Guid AnswerId,
    Guid PlayerId,
    Guid RoundId,
    Guid QuestionId,
    Guid AnswerOptionId) : DomainEvent;
