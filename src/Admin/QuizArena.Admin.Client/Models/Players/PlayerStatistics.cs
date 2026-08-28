namespace QuizArena.Admin.Client.Models.Players;

public enum TransactionType
{
    ANSWER_CORRECT = 0,
    ANSWER_INCORRECT = 1,
    ROUND_BONUS = 2,
    LEVEL_BONUS = 3,
    GAME_BONUS = 4,
    PENALTY = 5,
    WITHDRAWAL = 6,
    REWARD_REDEMPTION = 7,
    CONSOLATION = 8,
    ADJUSTMENT = 9
}

public static class TransactionTypeMap
{
    public static TransactionType FromApi(string? api) => api?.ToUpperInvariant() switch
    {
        "ANSWER_CORRECT" => TransactionType.ANSWER_CORRECT,
        "ANSWER_INCORRECT" => TransactionType.ANSWER_INCORRECT,
        "ROUND_BONUS" => TransactionType.ROUND_BONUS,
        "LEVEL_BONUS" => TransactionType.LEVEL_BONUS,
        "GAME_BONUS" => TransactionType.GAME_BONUS,
        "PENALTY" => TransactionType.PENALTY,
        "WITHDRAWAL" => TransactionType.WITHDRAWAL,
        "REWARD_REDEMPTION" => TransactionType.REWARD_REDEMPTION,
        "CONSOLATION" => TransactionType.CONSOLATION,
        "ADJUSTMENT" => TransactionType.ADJUSTMENT,
        _ => TransactionType.ANSWER_CORRECT
    };

    public static string ToApi(TransactionType t) => t.ToString().ToUpperInvariant();
    public static string DisplayName(TransactionType t) => t switch
    {
        TransactionType.ANSWER_CORRECT => "Acierto",
        TransactionType.ANSWER_INCORRECT => "Fallo",
        TransactionType.ROUND_BONUS => "Bono ronda",
        TransactionType.LEVEL_BONUS => "Bono nivel",
        TransactionType.GAME_BONUS => "Bono partida",
        TransactionType.PENALTY => "Penalización",
        TransactionType.WITHDRAWAL => "Retiro",
        TransactionType.REWARD_REDEMPTION => "Canje",
        TransactionType.CONSOLATION => "Consolación",
        TransactionType.ADJUSTMENT => "Ajuste",
        _ => t.ToString()
    };
}

public sealed record PointTransactionView(
    Guid TransactionId,
    Guid PlayerId,
    Guid GameId,
    TransactionType Type,
    int Points,
    DateTimeOffset Timestamp,
    Guid? ReferenceId);

public sealed record ScoreFilter(
    TransactionType? Type = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (From.HasValue && To.HasValue && From.Value > To.Value)
            errors["DateRange"] = ["Desde debe ser ≤ Hasta."];
        if (Page < 1) errors[nameof(Page)] = ["Page debe ser ≥1."];
        return errors;
    }
}

public sealed record PlayerRewardView(
    Guid RewardId,
    string RewardName,
    string RewardType,
    int Cost,
    string Status,
    bool IsEligible);

public sealed record PlayerRedemptionView(
    Guid RedemptionId,
    Guid RewardId,
    string RewardName,
    string RewardType,
    int Cost,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? DeliveredAt,
    string? Reason,
    bool IsConsolation,
    string RowVersion);

public sealed record RedemptionFilter(
    string? Status = null,
    string? RewardType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (From.HasValue && To.HasValue && From.Value > To.Value)
            errors["DateRange"] = ["Desde debe ser ≤ Hasta."];
        return errors;
    }
}

public sealed record PlayerStatistics(
    Guid PlayerId,
    int TotalGames,
    int Wins,
    int Top3,
    double AverageScore,
    double AccuracyRate,
    int BestStreak,
    TimeSpan AverageTimePerQuestion,
    IReadOnlyDictionary<string, int> DistributionByDifficulty,
    IReadOnlyDictionary<string, int> DistributionByCategory,
    DateTimeOffset CalculatedAt);
