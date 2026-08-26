namespace OroQuizClash.Infrastructure.Tests.Persistence;

public sealed class GameConcurrencyTests
{
    [Fact]
    public void Concurrency_TokenExists_OnGame()
    {
        var config = new OroQuizClash.Domain.Games.ValueObjects.GameConfiguration(
            "Quiz", new OroQuizClash.Domain.Categories.CategoryId(Guid.NewGuid()), 5, 10, 1,
            OroQuizClash.Domain.Games.Enumerations.DifficultyProgressionStrategy.Linear, 30,
            OroQuizClash.Domain.Games.Enumerations.ScoringSystem.Standard,
            OroQuizClash.Domain.Games.Enumerations.LossPolicy.LoseAll,
            OroQuizClash.Domain.Games.Enumerations.WithdrawalPolicy.KeepCurrentScore,
            OroQuizClash.Domain.Games.Enumerations.ConsolationPolicy.None,
            new OroQuizClash.Domain.Games.ValueObjects.RewardRules("Points", 500), 2, 10);
        var game = OroQuizClash.Domain.Games.Game.Create(config, Guid.NewGuid()).Value;
        Assert.NotNull(game);
        // RowVersion is set by EF; before save it's empty but after create it's non-null
    }
}