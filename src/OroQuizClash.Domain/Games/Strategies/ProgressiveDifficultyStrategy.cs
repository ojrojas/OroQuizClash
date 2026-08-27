using OroQuizClash.Domain.Games;

namespace OroQuizClash.Domain.Games.Strategies;

public sealed class ProgressiveDifficultyStrategy : IDifficultyProgressionStrategy
{
    public string Name => "Progressive";

    // Example curve: 1,1,2,3,5 for 5 rounds
    private static readonly int[] _curve = [1, 1, 2, 3, 5];

    public int NextDifficulty(Game game, int completedRounds)
    {
        if (completedRounds < _curve.Length)
            return Math.Clamp(_curve[completedRounds], 1, 5);

        // Beyond 5, stay at max 5
        return 5;
    }
}
