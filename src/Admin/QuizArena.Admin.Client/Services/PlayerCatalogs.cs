namespace QuizArena.Admin.Client.Services;

public static class PlayerCatalogs
{
    public static readonly IReadOnlyList<string> PlayerStates = ["Active", "InGame", "Withdrawn", "Inactive"];
    public static readonly IReadOnlyList<string> GameStatuses = ["DRAFT", "READY", "WAITING_FOR_PLAYERS", "IN_PROGRESS", "ROUND_IN_PROGRESS", "ROUND_COMPLETED", "FINISHED", "CANCELLED", "FORCED_FINISHED"];
    public static readonly IReadOnlyList<string> ParticipationStates = ["JOINED", "WITHDRAWN", "FINISHED", "KICKED"];
    public static readonly IReadOnlyList<string> TransactionTypes = ["ANSWER_CORRECT", "ANSWER_INCORRECT", "ROUND_BONUS", "LEVEL_BONUS", "GAME_BONUS", "PENALTY", "WITHDRAWAL", "REWARD_REDEMPTION", "CONSOLATION", "ADJUSTMENT"];
    public static readonly IReadOnlyList<string> RedemptionStatuses = ["Requested", "Approved", "Rejected", "Delivered", "Cancelled"];
    public static readonly IReadOnlyList<string> RewardTypes = ["Monetary", "Physical", "Digital", "Voucher", "Experience", "Consolation"];

    public static IReadOnlyList<(string Value, string Label)> PlayerStateOptions =>
        PlayerStates.Select(s => (s, s)).ToList();

    public static IReadOnlyList<(string Value, string Label)> GameStatusOptions =>
        GameStatuses.Select(s => (s, s)).ToList();

    public static IReadOnlyList<(string Value, string Label)> TransactionTypeOptions =>
        TransactionTypes.Select(t => (t, t)).ToList();

    public static bool IsValidPlayerState(string? state) =>
        state is null || PlayerStates.Contains(state, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidGameStatus(string? status) =>
        status is null || GameStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidTransactionType(string? type) =>
        type is null || TransactionTypes.Contains(type, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidDateRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from.HasValue && to.HasValue) return from.Value <= to.Value;
        return true;
    }
}
