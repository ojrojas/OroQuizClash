using BuildingBlocks.Kernel.Domain.Entities;

namespace OroQuizClash.Domain.Games;

public sealed class GamePlayer : Entity<GamePlayerId>
{
    public GameId GameId { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public string? DisplayName { get; private set; }

    private GamePlayer() { }

    internal GamePlayer(GamePlayerId id, GameId gameId, Guid userId, string? displayName = null)
        : base(id)
    {
        GameId = gameId;
        UserId = userId;
        DisplayName = displayName;
        JoinedAt = DateTimeOffset.UtcNow;
    }
}
