namespace QuizArena.Admin.Client.Models.GameConfiguration;

/// <summary>
/// 8-state administrative view (spec 019) mapped to domain states (research R1).
/// </summary>
public enum GameStateView
{
    Draft,
    Configured,
    Scheduled,
    Ready,
    Running,
    Paused,
    Finished,
    Cancelled
}

public static class GameStateViewMap
{
    public static GameStateView FromApi(string? apiStatus) => apiStatus?.ToUpperInvariant() switch
    {
        "DRAFT" => GameStateView.Draft,
        "CONFIGURED" => GameStateView.Configured,
        "SCHEDULED" => GameStateView.Scheduled,
        "READY" => GameStateView.Ready,
        "WAITING_FOR_PLAYERS" => GameStateView.Scheduled,
        "IN_PROGRESS" => GameStateView.Running,
        "ROUND_IN_PROGRESS" => GameStateView.Running,
        "ROUND_COMPLETED" => GameStateView.Running,
        "PAUSED" => GameStateView.Paused,
        "FINISHED" => GameStateView.Finished,
        "FORCED_FINISHED" => GameStateView.Finished,
        "CANCELLED" => GameStateView.Cancelled,
        _ => GameStateView.Draft
    };

    public static string ToApi(GameStateView state) => state switch
    {
        GameStateView.Draft => "DRAFT",
        GameStateView.Configured => "CONFIGURED",
        GameStateView.Scheduled => "SCHEDULED",
        GameStateView.Ready => "READY",
        GameStateView.Running => "IN_PROGRESS",
        GameStateView.Paused => "PAUSED",
        GameStateView.Finished => "FINISHED",
        GameStateView.Cancelled => "CANCELLED",
        _ => "DRAFT"
    };

    public static string ToDisplayName(GameStateView state) => state switch
    {
        GameStateView.Draft => "Draft",
        GameStateView.Configured => "Configured",
        GameStateView.Scheduled => "Scheduled",
        GameStateView.Ready => "Ready",
        GameStateView.Running => "Running",
        GameStateView.Paused => "Paused",
        GameStateView.Finished => "Finished",
        GameStateView.Cancelled => "Cancelled",
        _ => state.ToString()
    };

    public static bool IsTerminal(GameStateView state) =>
        state is GameStateView.Finished or GameStateView.Cancelled;

    public static bool CanEdit(GameStateView state) =>
        state is GameStateView.Draft or GameStateView.Configured or GameStateView.Scheduled;

    public static bool IsImmutable(GameStateView state) =>
        state is GameStateView.Ready or GameStateView.Running or GameStateView.Paused;
}
