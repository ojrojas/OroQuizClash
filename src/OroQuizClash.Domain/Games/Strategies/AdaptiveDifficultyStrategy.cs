using OroQuizClash.Domain.Games;

namespace OroQuizClash.Domain.Games.Strategies;

public sealed class AdaptiveDifficultyStrategy : IDifficultyProgressionStrategy
{
    public string Name => "Adaptive";

    // For demo, simple adaptive based on completed rounds parity; real would use PointTransaction avg
    public int NextDifficulty(Game game, int completedRounds)
    {
        // If many rounds completed, stay high, else moderate
        var initial = game.Configuration.InitialDifficulty;
        if (completedRounds == 0) return initial;
        if (completedRounds % 2 == 0) return Math.Clamp(initial + 1, 1, 5);
        return Math.Clamp(initial + completedRounds / 2, 1, 5);
    }
}
