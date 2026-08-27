using BuildingBlocks.Kernel.Domain.Events;

using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Games.Events;

public sealed record AnswerEvaluatedDomainEvent(
    Guid GameId,
    Guid AnswerId,
    Guid PlayerId,
    Guid RoundId,
    bool Correct,
    int Points,
    int ElapsedTime,
    AnswerStatus Status) : DomainEvent;
