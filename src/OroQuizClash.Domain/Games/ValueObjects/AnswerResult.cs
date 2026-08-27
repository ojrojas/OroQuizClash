using BuildingBlocks.Kernel.Domain.ValueObjects;

using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Games.ValueObjects;

public sealed class AnswerResult : ValueObject
{
    public AnswerId AnswerId { get; }
    public bool Correct { get; }
    public int Points { get; }
    public int ElapsedTime { get; }
    public AnswerStatus Status { get; }

    public AnswerResult(AnswerId answerId, bool correct, int points, int elapsedTime, AnswerStatus status)
    {
        AnswerId = answerId;
        Correct = correct;
        Points = points;
        ElapsedTime = elapsedTime;
        Status = status;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AnswerId;
        yield return Correct;
        yield return Points;
        yield return ElapsedTime;
        yield return Status;
    }
}
