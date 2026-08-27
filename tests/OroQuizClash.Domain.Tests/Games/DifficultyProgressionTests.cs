using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Strategies;
using OroQuizClash.Domain.Games.ValueObjects;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class DifficultyProgressionTests
{
    private static GameConfiguration Config(int initialDifficulty = 1)
    {
        return new GameConfiguration(
            "Quiz",
            new CategoryId(Guid.NewGuid()),
            5, 10, initialDifficulty,
            DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseAll,
            WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None,
            new RewardRules("Points", 1000),
            2, 10);
    }

    private static Domain.Games.Game Game(int initialDifficulty = 1)
    {
        return Domain.Games.Game.Create(Config(initialDifficulty), Guid.NewGuid()).Value;
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    [InlineData(4, 5)]
    [InlineData(5, 5)]
    public void Linear_NextDifficulty_IncrementsAndClamps(int completedRounds, int expected)
    {
        var strategy = new LinearDifficultyStrategy();
        var game = Game(initialDifficulty: 1);

        var result = strategy.NextDifficulty(game, completedRounds);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 5)]
    [InlineData(5, 5)]
    public void Progressive_NextDifficulty_FollowsCurve(int completedRounds, int expected)
    {
        var strategy = new ProgressiveDifficultyStrategy();
        var game = Game();

        var result = strategy.NextDifficulty(game, completedRounds);

        Assert.Equal(expected, result);
    }
}
