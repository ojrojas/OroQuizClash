namespace QuizArena.Admin.Client.Services;

public static class AuditCatalogs
{
    public static readonly IReadOnlyList<string> EntityTypes = ["Game", "Category", "Question", "GamePlayer", "Reward", "RewardRedemption", "Player"];
    public static readonly IReadOnlyList<string> Actions = ["CREATE", "UPDATE", "DELETE", "ACTIVATE", "DEACTIVATE", "ARCHIVE", "APPROVE", "REJECT", "DELIVER", "CANCEL", "START", "FINISH", "JOIN", "WITHDRAW"];
    public static readonly IReadOnlyList<string> Results = ["Success", "Failed"];
    public static readonly IReadOnlyList<string> ErrorCodes = ["ConcurrencyConflict", "RewardAlreadyExists", "InvalidFilter", "CategoryNotReady", "RewardOutOfStock", "InvalidAction"];

    public static bool IsValidEntityType(string? entityType) =>
        entityType is null || EntityTypes.Contains(entityType, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidAction(string? action) =>
        action is null || Actions.Contains(action, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidResult(string? result) =>
        result is null || Results.Contains(result, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidErrorCode(string? code) =>
        code is null || ErrorCodes.Contains(code, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<(string Value, string Label)> EntityTypeOptions =>
        EntityTypes.Select(e => (e, e)).ToList();

    public static IReadOnlyList<(string Value, string Label)> ActionOptions =>
        Actions.Select(a => (a, a)).ToList();

    public static IReadOnlyList<(string Value, string Label)> ResultOptions =>
        Results.Select(r => (r, r)).ToList();
}
