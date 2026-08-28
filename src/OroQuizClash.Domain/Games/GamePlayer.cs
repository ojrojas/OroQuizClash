using BuildingBlocks.Kernel.Domain.Entities;

using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;

namespace OroQuizClash.Domain.Games;

public sealed class GamePlayer : Entity<GamePlayerId>
{
    public GameId GameId { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public string? DisplayName { get; private set; }
    public PlayerScore Score { get; private set; } = PlayerScore.Zero();
    public PlayerParticipationStatus ParticipationStatus { get; private set; } = PlayerParticipationStatus.Active;
    public int CurrentRoundNumber { get; private set; }
    public DateTimeOffset? ExitedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsWithdrawn => ParticipationStatus == PlayerParticipationStatus.Withdrawn;
    public bool IsActive => ParticipationStatus == PlayerParticipationStatus.Active;

    private GamePlayer() { }

    internal GamePlayer(GamePlayerId id, GameId gameId, Guid userId, string? displayName = null)
        : base(id)
    {
        GameId = gameId;
        UserId = userId;
        DisplayName = displayName;
        JoinedAt = DateTimeOffset.UtcNow;
        Score = PlayerScore.Zero();
        ParticipationStatus = PlayerParticipationStatus.Active;
    }

    internal void UpdateScore(PlayerScore newScore)
    {
        Score = newScore;
    }

    internal void AdvanceToRound(int roundNumber)
    {
        if (!IsActive) return;
        if (roundNumber <= CurrentRoundNumber) return;
        CurrentRoundNumber = roundNumber;
    }

    internal void MarkWithdrawn()
    {
        ParticipationStatus = PlayerParticipationStatus.Withdrawn;
        ExitedAt = DateTimeOffset.UtcNow;
    }

    internal void MarkEliminated()
    {
        ParticipationStatus = PlayerParticipationStatus.Eliminated;
        ExitedAt = DateTimeOffset.UtcNow;
    }

    internal void MarkWinner()
    {
        ParticipationStatus = PlayerParticipationStatus.Winner;
    }
}
