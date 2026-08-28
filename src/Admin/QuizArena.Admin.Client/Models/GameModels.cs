namespace QuizArena.Admin.Client.Models;

public enum GameStatusView
{
    Configuring,
    Lobby,
    Active,
    Finished,
    Cancelled
}

public static class GameStatusMap
{
    public static GameStatusView FromApi(string? apiStatus) => apiStatus?.ToUpperInvariant() switch
    {
        "DRAFT" or "READY" => GameStatusView.Configuring,
        "WAITING_FOR_PLAYERS" => GameStatusView.Lobby,
        "IN_PROGRESS" or "ROUND_IN_PROGRESS" or "ROUND_COMPLETED" => GameStatusView.Active,
        "FINISHED" or "FORCED_FINISHED" => GameStatusView.Finished,
        "CANCELLED" => GameStatusView.Cancelled,
        _ => GameStatusView.Configuring
    };

    public static string? ToApiQuery(GameStatusView? status) => status switch
    {
        GameStatusView.Configuring => "DRAFT",
        GameStatusView.Lobby => "WAITING_FOR_PLAYERS",
        GameStatusView.Active => "IN_PROGRESS",
        GameStatusView.Finished => "FINISHED",
        GameStatusView.Cancelled => "CANCELLED",
        _ => null
    };

    public static string DisplayName(GameStatusView status) => status switch
    {
        GameStatusView.Configuring => "Configuring",
        GameStatusView.Lobby => "Lobby",
        GameStatusView.Active => "Active",
        GameStatusView.Finished => "Finished",
        GameStatusView.Cancelled => "Cancelled",
        _ => status.ToString()
    };
}

public sealed record GameSummary(
    Guid Id,
    string Name,
    Guid CategoryId,
    GameStatusView Status,
    int MinRounds,
    int MaxRounds,
    int PlayerCount,
    int RoundCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);

public sealed record GameDetail(
    Guid Id,
    string Name,
    Guid CategoryId,
    GameStatusView Status,
    int MinRounds,
    int MaxRounds,
    int PlayerCount,
    int RoundCount,
    string RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<LeaderboardEntry> Leaderboard);

public sealed record RoundSummary(int RoundNumber, string Status, DateTimeOffset? CompletedAt);

public sealed record LeaderboardEntry(
    Guid PlayerId,
    string PlayerName,
    int Rank,
    int Score,
    int SecuredPoints,
    string Status,
    bool IsCurrentOperator);

public sealed record GameConfigurationForm(
    string Name,
    string? Description,
    Guid CategoryId,
    int Difficulty,
    int Rounds,
    int QuestionsPerRound,
    int TimeLimitSeconds,
    int MinPlayers,
    int MaxPlayers,
    decimal? EntryFee,
    decimal? RewardPool,
    string DifficultyStrategy = "Linear",
    string ScoringSystem = "Standard",
    string LossPolicy = "LOSE_ALL",
    string WithdrawalPolicy = "KEEP_SECURED_SCORE",
    string ConsolationPolicy = "None",
    string RewardType = "None",
    int RewardThreshold = 0)
{
    public static readonly string[] DifficultyStrategies = ["Linear", "Progressive", "Adaptive", "CategorySpecific"];
    public static readonly string[] ScoringSystems = ["Standard", "ProgressiveBonus"];
    public static readonly string[] LossPolicies = ["LOSE_ALL", "LOSE_CURRENT_ROUND", "LOSE_UNSECURED_POINTS", "FALLBACK_TO_CHECKPOINT"];
    public static readonly string[] WithdrawalPolicies = ["LOSE_ALL", "KEEP_CURRENT_SCORE", "KEEP_SECURED_SCORE", "KEEP_CHECKPOINT_SCORE"];
    public static readonly string[] ConsolationPolicies = ["None", "FixedPoints", "RewardBased", "ParticipationBased"];

    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        var name = Name?.Trim() ?? string.Empty;
        if (name.Length < 3 || name.Length > 100)
            errors[nameof(Name)] = ["Name must be 3-100 characters."];
        if (Description is not null && Description.Length > 500)
            errors[nameof(Description)] = ["Description must be at most 500 characters."];
        if (CategoryId == Guid.Empty)
            errors[nameof(CategoryId)] = ["Category is required."];
        if (Difficulty is < 1 or > 5)
            errors[nameof(Difficulty)] = ["Difficulty must be 1-5."];
        if (Rounds is < 5 or > 10)
            errors[nameof(Rounds)] = ["Rounds must be 5-10 (backend requires at least 5)."];
        if (QuestionsPerRound is < 1 or > 20)
            errors[nameof(QuestionsPerRound)] = ["Questions per round must be 1-20."];
        if (TimeLimitSeconds is < 5 or > 300)
            errors[nameof(TimeLimitSeconds)] = ["Time limit must be 5-300 seconds."];
        if (MinPlayers < 1)
            errors[nameof(MinPlayers)] = ["Minimum players must be at least 1."];
        if (MaxPlayers < 1 || MaxPlayers < MinPlayers)
            errors[nameof(MaxPlayers)] = ["Maximum players must be at least the minimum."];
        if (EntryFee is < 0)
            errors[nameof(EntryFee)] = ["Entry fee must be zero or positive."];
        if (RewardPool is < 0)
            errors[nameof(RewardPool)] = ["Reward pool must be zero or positive."];
        return errors;
    }

    public bool IsValid => Validate().Count == 0;
}
