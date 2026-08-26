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

    [Fact]
    public void Start_FromDraft_Succeeds()
    {
        var game = CreateValid();
        var result = game.Start();
        Assert.True(result.IsSuccess);
        Assert.Equal(GameStatus.WaitingForPlayers, game.Status);
    }

    [Fact]
    public void Start_Twice_FailsWithImmutable()
    {
        var game = CreateValid();
        game.Start();
        var second = game.Start();
        Assert.True(second.IsFailure);
        Assert.Equal("InvalidGameState.ConfigurationImmutable", second.Error.Code);
    }

    [Fact]
    public void UpdateConfiguration_AfterStart_Fails()
    {
        var game = CreateValid();
        game.Start();
        var newConfig = new GameConfiguration(
            "NewName",
            new CategoryId(Guid.NewGuid()),
            5, 10, 1, DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseAll, WithdrawalPolicy.KeepCurrentScore,
            ConsolationPolicy.None, new RewardRules("Points", 500), 2, 10);
        var result = game.UpdateConfiguration(newConfig);
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidGameState.ConfigurationImmutable", result.Error.Code);
    }
}