using BuildingBlocks.Kernel.Domain.Entities;

using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Rules;
using OroQuizClash.Domain.Questions;

namespace OroQuizClash.Domain.Games;

public sealed class Answer : Entity<AnswerId>
{
    public GameId GameId { get; private set; } = null!;
    public Guid PlayerId { get; private set; }
    public GameRoundId RoundId { get; private set; } = null!;
    public QuestionId QuestionId { get; private set; } = null!;
    public AnswerOptionId AnswerOptionId { get; private set; } = null!;
    public AnswerStatus Status { get; private set; } = AnswerStatus.NotAnswered;
    public bool? Correct { get; private set; }
    public int Points { get; private set; }
    public int ElapsedTime { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EvaluatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private Answer() { }

    internal Answer(
        AnswerId id,
        GameId gameId,
        Guid playerId,
        GameRoundId roundId,
        QuestionId questionId,
        AnswerOptionId answerOptionId)
        : base(id)
    {
        GameId = gameId;
        PlayerId = playerId;
        RoundId = roundId;
        QuestionId = questionId;
        AnswerOptionId = answerOptionId;
        Status = AnswerStatus.NotAnswered;
        Points = 0;
        ElapsedTime = 0;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    internal void Submit()
    {
        var rule = new AnswerImmutabilityRule(Status);
        if (rule.IsBroken())
            throw new InvalidOperationException("Answer cannot be modified after evaluation.");

        if (!AnswerStatus.IsValidTransition(Status, AnswerStatus.Answered))
            throw new InvalidOperationException($"Cannot transition from {Status.Name} to ANSWERED.");

        Status = AnswerStatus.Answered;
    }

    internal void Evaluate(bool correct, int points, int elapsedTime)
    {
        var rule = new AnswerImmutabilityRule(Status);
        if (rule.IsBroken())
            throw new InvalidOperationException("Answer cannot be modified after evaluation.");

        if (!AnswerStatus.IsValidTransition(Status, AnswerStatus.Evaluated))
            throw new InvalidOperationException($"Cannot transition from {Status.Name} to EVALUATED.");

        Status = AnswerStatus.Evaluated;
        Correct = correct;
        Points = points;
        ElapsedTime = elapsedTime;
        EvaluatedAt = DateTimeOffset.UtcNow;
    }

    internal void Expire(int timeLimit)
    {
        var rule = new AnswerImmutabilityRule(Status);
        if (rule.IsBroken())
            throw new InvalidOperationException("Answer cannot be modified after evaluation.");

        if (!AnswerStatus.IsValidTransition(Status, AnswerStatus.Expired))
            throw new InvalidOperationException($"Cannot transition from {Status.Name} to EXPIRED.");

        Status = AnswerStatus.Expired;
        Correct = null;
        Points = 0;
        ElapsedTime = timeLimit;
        EvaluatedAt = DateTimeOffset.UtcNow;
    }
}
