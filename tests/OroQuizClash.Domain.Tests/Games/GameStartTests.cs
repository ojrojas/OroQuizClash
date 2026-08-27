using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class GameStartTests
{
    private static Domain.Games.Game CreateValid()
    {
        var config = new GameConfiguration(
            "Quiz",
            new CategoryId(Guid.NewGuid()),
            5, 10, 1, DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseAll, WithdrawalPolicy.KeepCurrentScore,
            ConsolationPolicy.None, new RewardRules("Points", 500), 2, 10);
        var result = Domain.Games.Game.Create(config, Guid.NewGuid());
        return result.Value;
    }

    private static Domain.Games.Game CreateReadyWithPlayers()
    {
        var game = CreateValid();
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        game.JoinPlayer(Guid.NewGuid());
        game.JoinPlayer(Guid.NewGuid());
        return game;
    }

    [Fact]
    public void Start_FromWaitingForPlayers_Succeeds()
    {
        var game = CreateReadyWithPlayers();
        var result = game.Start();
        Assert.True(result.IsSuccess);
        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.NotNull(game.StartedAt);
    }

    [Fact]
    public void Start_FromDraft_Fails()
    {
        var game = CreateValid();
        var result = game.Start();
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Start_WithNotEnoughPlayers_Fails()
    {
        var game = CreateValid();
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        game.JoinPlayer(Guid.NewGuid());
        var result = game.Start();
        Assert.True(result.IsFailure);
        Assert.Equal("NotEnoughPlayers", result.Error.Code);
    }

    [Fact]
    public void Start_Twice_FailsWithInvalidState()
    {
        var game = CreateReadyWithPlayers();
        game.Start();
        var second = game.Start();
        Assert.True(second.IsFailure);
    }

    [Fact]
    public void UpdateConfiguration_AfterStart_Fails()
    {
        var game = CreateReadyWithPlayers();
        game.Start();
        var newConfig = new GameConfiguration(
            "NewName",
            new CategoryId(Guid.NewGuid()),
            5, 10, 1, DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseAll, WithdrawalPolicy.KeepCurrentScore,
            ConsolationPolicy.None, new RewardRules("Points", 500), 2, 10);
        var result = game.UpdateConfiguration(newConfig);
        Assert.True(result.IsFailure);
    }
}
