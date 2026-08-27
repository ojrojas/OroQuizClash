using OroQuizClash.Domain.Games;

namespace OroQuizClash.Domain.Games.Strategies;

public interface IDifficultyProgressionStrategy
{
    int NextDifficulty(Game game, int completedRounds);
    string Name { get; }
}
