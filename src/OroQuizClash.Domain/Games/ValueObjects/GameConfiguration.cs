using BuildingBlocks.Kernel.Domain.ValueObjects;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Games.ValueObjects;

public sealed class GameConfiguration : ValueObject
{
    public string Name { get; }
    public CategoryId CategoryId { get; }
    public int MinRounds { get; }
    public int MaxRounds { get; }
    public int InitialDifficulty { get; }
    public DifficultyProgressionStrategy DifficultyStrategy { get; }
    public int TimeLimitPerQuestionSeconds { get; }
    public ScoringSystem ScoringSystem { get; }
    public LossPolicy LossPolicy { get; }
    public WithdrawalPolicy WithdrawalPolicy { get; }
    public ConsolationPolicy ConsolationPolicy { get; }
    public RewardRules RewardRules { get; }
    public int MinPlayers { get; }
    public int MaxPlayers { get; }
    public int PointsPerRound { get; }

    public GameConfiguration(
        string name,
        CategoryId categoryId,
        int minRounds,
        int maxRounds,
        int initialDifficulty,
        DifficultyProgressionStrategy difficultyStrategy,
        int timeLimitPerQuestionSeconds,
        ScoringSystem scoringSystem,
        LossPolicy lossPolicy,
        WithdrawalPolicy withdrawalPolicy,
        ConsolationPolicy consolationPolicy,
        RewardRules rewardRules,
        int minPlayers,
        int maxPlayers,
        int pointsPerRound = 10)
    {
        Name = name;
        CategoryId = categoryId;
        MinRounds = minRounds;
        MaxRounds = maxRounds;
        InitialDifficulty = initialDifficulty;
        DifficultyStrategy = difficultyStrategy;
        TimeLimitPerQuestionSeconds = timeLimitPerQuestionSeconds;
        ScoringSystem = scoringSystem;
        LossPolicy = lossPolicy;
        WithdrawalPolicy = withdrawalPolicy;
        ConsolationPolicy = consolationPolicy;
        RewardRules = rewardRules;
        MinPlayers = minPlayers;
        MaxPlayers = maxPlayers;
        PointsPerRound = pointsPerRound;
    }

    private GameConfiguration()
    {
        Name = string.Empty;
        CategoryId = new CategoryId(Guid.Empty);
        DifficultyStrategy = DifficultyProgressionStrategy.Linear;
        ScoringSystem = ScoringSystem.Standard;
        LossPolicy = LossPolicy.LoseAll;
        WithdrawalPolicy = WithdrawalPolicy.LoseAll;
        ConsolationPolicy = ConsolationPolicy.None;
        RewardRules = new RewardRules("Points", 0);
        PointsPerRound = 10;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return CategoryId;
        yield return MinRounds;
        yield return MaxRounds;
        yield return InitialDifficulty;
        yield return DifficultyStrategy;
        yield return TimeLimitPerQuestionSeconds;
        yield return ScoringSystem;
        yield return LossPolicy;
        yield return WithdrawalPolicy;
        yield return ConsolationPolicy;
        yield return RewardRules;
        yield return MinPlayers;
        yield return MaxPlayers;
        yield return PointsPerRound;
    }
}