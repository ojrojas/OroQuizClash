using BuildingBlocks.Kernel.Domain.Entities;

using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Questions;

namespace OroQuizClash.Domain.Games;

public sealed class PointTransaction : Entity<PointTransactionId>
{
    public GameId GameId { get; private set; } = null!;
    public Guid PlayerId { get; private set; }
    public GameRoundId? RoundId { get; private set; }
    public QuestionId? QuestionId { get; private set; }
    public AnswerId? AnswerId { get; private set; }
    public PointTransactionType Type { get; private set; } = null!;
    public int Points { get; private set; }
    public int ResultingBalance { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PointTransaction() { }

    internal PointTransaction(
        PointTransactionId id,
        GameId gameId,
        Guid playerId,
        GameRoundId? roundId,
        QuestionId? questionId,
        AnswerId? answerId,
        PointTransactionType type,
        int points,
        int resultingBalance,
        string? reason = null)
        : base(id)
    {
        GameId = gameId;
        PlayerId = playerId;
        RoundId = roundId;
        QuestionId = questionId;
        AnswerId = answerId;
        Type = type;
        Points = points;
        ResultingBalance = resultingBalance;
        Reason = reason;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
