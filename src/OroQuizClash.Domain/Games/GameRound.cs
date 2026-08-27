using BuildingBlocks.Kernel.Domain.Entities;

using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Questions;

namespace OroQuizClash.Domain.Games;

public sealed class GameRound : Entity<GameRoundId>
{
    public GameId GameId { get; private set; } = null!;
    public int RoundNumber { get; private set; }
    public QuestionId QuestionId { get; private set; } = null!;
    public GameStatus Status { get; private set; } = GameStatus.RoundInProgress;
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private GameRound() { }

    internal GameRound(GameRoundId id, GameId gameId, int roundNumber, QuestionId questionId)
        : base(id)
    {
        GameId = gameId;
        RoundNumber = roundNumber;
        QuestionId = questionId;
        Status = GameStatus.RoundInProgress;
        StartedAt = DateTimeOffset.UtcNow;
    }

    internal void Complete()
    {
        if (Status != GameStatus.RoundInProgress)
            throw new InvalidOperationException($"Cannot complete round in status {Status.Name}");
        Status = GameStatus.RoundCompleted;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
