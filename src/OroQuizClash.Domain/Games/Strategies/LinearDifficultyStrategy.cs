using OroQuizClash.Domain.Games;

namespace OroQuizClash.Domain.Games.Strategies;

public sealed class LinearDifficultyStrategy : IDifficultyProgressionStrategy
{
    public string Name => "Linear";

    public int NextDifficulty(Game game, int completedRounds)
    {
        var initial = game.Configuration.InitialDifficulty;
        return Math.Clamp(initial + completedRounds, 1, 5);
    }
}
