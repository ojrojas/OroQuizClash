using BuildingBlocks.Kernel.Domain.Entities;

using OroQuizClash.Domain.Games.ValueObjects;

namespace OroQuizClash.Domain.Games;

public sealed class GamePlayer : Entity<GamePlayerId>
{
    public GameId GameId { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public string? DisplayName { get; private set; }
    public PlayerScore Score { get; private set; } = PlayerScore.Zero();
    public bool IsWithdrawn { get; private set; }
    public DateTimeOffset? WithdrawnAt { get; private set; }

    private GamePlayer() { }

    internal GamePlayer(GamePlayerId id, GameId gameId, Guid userId, string? displayName = null)
        : base(id)
    {
        GameId = gameId;
        UserId = userId;
        DisplayName = displayName;
        JoinedAt = DateTimeOffset.UtcNow;
        Score = PlayerScore.Zero();
        IsWithdrawn = false;
    }

    internal void UpdateScore(PlayerScore newScore)
    {
        Score = newScore;
    }

    internal void MarkWithdrawn()
    {
        IsWithdrawn = true;
        WithdrawnAt = DateTimeOffset.UtcNow;
    }
}
