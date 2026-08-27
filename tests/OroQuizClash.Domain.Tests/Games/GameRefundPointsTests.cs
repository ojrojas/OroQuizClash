using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Events;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Categories;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class GameRefundPointsTests
{
    [Fact]
    public void RefundPoints_CreditsBalance()
    {
        var game = CreateGameWithPlayer(out var playerId, 200);
        game.ConsumePoints(playerId, 100, "test");

        var result = game.RefundPoints(playerId, 100, "Refund for test");

        Assert.True(result.IsSuccess);
        Assert.Equal(200, game.GetPlayerScore(playerId).CurrentPoints);
        Assert.Equal(PointTransactionType.Adjustment, result.Value.Type);
        Assert.Equal(100, result.Value.Points);
    }

    [Fact]
    public void RefundPoints_AppendsLedgerEntry()
    {
        var game = CreateGameWithPlayer(out var playerId, 200);

        var result = game.RefundPoints(playerId, 50, "Refund for redemption");

        Assert.True(result.IsSuccess);
        Assert.Contains(game.PointTransactions, pt =>
            pt.Type == PointTransactionType.Adjustment &&
            pt.Points == 50 &&
            pt.Reason == "Refund for redemption");
    }

    [Fact]
    public void RefundPoints_ZeroAmount_ReturnsFailure()
    {
        var game = CreateGameWithPlayer(out var playerId, 200);

        var result = game.RefundPoints(playerId, 0, "reason");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void RefundPoints_UnknownPlayer_ReturnsFailure()
    {
        var game = CreateGameWithPlayer(out _, 200);

        var result = game.RefundPoints(Guid.NewGuid(), 50, "reason");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void RefundPoints_RegardlessOfGameState()
    {
        var game = CreateGameWithPlayer(out var playerId, 200);
        game.Finish();

        var result = game.RefundPoints(playerId, 50, "post-finish refund");

        Assert.True(result.IsSuccess);
    }

    private static Game CreateGameWithPlayer(out Guid playerId, int initialPoints)
    {
        playerId = Guid.NewGuid();
        var config = new GameConfiguration(
            "Test Game",
            new CategoryId(Guid.NewGuid()),
            5, 10, 1,
            DifficultyProgressionStrategy.Linear,
            30,
            ScoringSystem.Standard,
            LossPolicy.LoseAll,
            WithdrawalPolicy.KeepCurrentScore,
            ConsolationPolicy.None,
            new RewardRules("Points", 1000),
            2, 10, 100);
        var game = Game.Create(config, Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        game.JoinPlayer(playerId, "Player1");
        game.Start();

        if (initialPoints > 0)
            game.AdjustPoints(playerId, initialPoints, "Test setup", Guid.NewGuid());

        return game;
    }
}
