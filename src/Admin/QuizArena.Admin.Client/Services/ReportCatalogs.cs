namespace QuizArena.Admin.Client.Services;

public static class ReportCatalogs
{
    public static readonly IReadOnlyList<string> GameStatuses = ["DRAFT", "READY", "WAITING_FOR_PLAYERS", "IN_PROGRESS", "ROUND_IN_PROGRESS", "ROUND_COMPLETED", "FINISHED", "CANCELLED", "FORCED_FINISHED"];
    public static readonly IReadOnlyList<int> Levels = [1, 2, 3, 4, 5];
    public static readonly IReadOnlyList<string> Results = ["FINISHED", "CANCELLED", "WITHDRAWN", "Approved", "Rejected", "Correct", "Incorrect", "DRAFT", "READY"];
    public static readonly IReadOnlyList<string> TransactionTypes = ["ANSWER_CORRECT", "ANSWER_INCORRECT", "ROUND_BONUS", "LEVEL_BONUS", "GAME_BONUS", "PENALTY", "WITHDRAWAL", "REWARD_REDEMPTION", "CONSOLATION", "ADJUSTMENT"];
    public static readonly IReadOnlyList<string> RewardTypes = ["Monetary", "Physical", "Digital", "Voucher", "Experience", "Consolation"];
    public static readonly IReadOnlyList<string> RedemptionStatuses = ["Requested", "Approved", "Rejected", "Delivered", "Cancelled"];

    public static bool IsValidResult(string? result) =>
        result is null || Results.Contains(result, StringComparer.OrdinalIgnoreCase) || GameStatuses.Contains(result, StringComparer.OrdinalIgnoreCase) || RedemptionStatuses.Contains(result, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidLevel(int? level) =>
        level is null || Levels.Contains(level.Value);

    public static bool IsValidGameStatus(string? status) =>
        status is null || GameStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidDateRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from.HasValue && to.HasValue) return from.Value <= to.Value;
        return true;
    }

    public static IReadOnlyList<(string Value, string Label)> GameStatusOptions =>
        GameStatuses.Select(s => (s, s)).ToList();

    public static IReadOnlyList<(string Value, string Label)> LevelOptions =>
        Levels.Select(l => (l.ToString(), $"Nivel {l}")).ToList();

    public static IReadOnlyList<(string Value, string Label)> ResultOptions =>
        Results.Select(r => (r, r)).ToList();
}
