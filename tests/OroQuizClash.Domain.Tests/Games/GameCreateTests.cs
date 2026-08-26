using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class GameCreateTests
{
    private static GameConfiguration ValidConfig(string name = "Quiz Masters", int minRounds = 5, int time = 30, Guid? cat = null)
    {
        return new GameConfiguration(
            name,
            new CategoryId(cat ?? Guid.NewGuid()),
            minRounds,
            10,
            1,
            DifficultyProgressionStrategy.Linear,
            time,
            ScoringSystem.Standard,
            LossPolicy.LoseAll,
            WithdrawalPolicy.KeepCurrentScore,
            ConsolationPolicy.None,
            new RewardRules("Points", 1000),
            2,
            10);
    }

    [Fact]
    public void Create_WithValidConfig_Succeeds()
    {
        var config = ValidConfig();
        var result = Domain.Games.Game.Create(config, Guid.NewGuid());
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Quiz Masters", result.Value.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void Create_WithInvalidName_Fails(string name)
    {
        var config = ValidConfig(name);
        var result = Domain.Games.Game.Create(config, Guid.NewGuid());
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidGameConfiguration.InvalidName", result.Error.Code);
    }

    [Fact]
    public void Create_WithMinRoundsLessThan5_Fails()
    {
        var config = ValidConfig(minRounds: 3);
        var result = Domain.Games.Game.Create(config, Guid.NewGuid());
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidGameConfiguration.MinRoundsTooLow", result.Error.Code);
    }

    [Fact]
    public void Create_WithInvalidTime_Fails()
    {
        var config = ValidConfig(time: 0);
        var result = Domain.Games.Game.Create(config, Guid.NewGuid());
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidGameConfiguration.InvalidTimeLimit", result.Error.Code);
    }

    [Fact]
    public void Create_WithEmptyCategory_Fails()
    {
        var config = ValidConfig(cat: Guid.Empty);
        var result = Domain.Games.Game.Create(config, Guid.NewGuid());
        Assert.True(result.IsFailure);
    }
}