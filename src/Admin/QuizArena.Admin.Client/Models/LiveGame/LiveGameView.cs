namespace QuizArena.Admin.Client.Models.LiveGame;

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
}

public sealed record LiveGameView(
    Guid GameId,
    GameStateView Status,
    int CurrentRound,
    QuestionView? CurrentQuestion,
    int TotalRounds,
    int Players,
    int PlayersConnected,
    int PlayersAnswered,
    int PlayersWaiting,
    IReadOnlyList<LiveScore> Scores,
    int CurrentLevel,
    int RemainingSeconds,
    string RowVersion,
    DateTimeOffset LastUpdated);

public sealed record QuestionView(
    Guid QuestionId,
    string Text,
    IReadOnlyList<AnswerView> Options,
    string? CorrectAnswer);

public sealed record AnswerView(Guid OptionId, string Text, char Position);

public sealed record LiveScore(
    Guid PlayerId,
    string DisplayName,
    int Score,
    int SecuredPoints,
    int Level,
    bool HasAnswered);

public sealed record GameRoundState(
    int RoundNumber,
    string Status,
    DateTimeOffset StartedAt,
    Guid? QuestionId);

public sealed record PlayerPresence(
    Guid GameId,
    int TotalPlayers,
    int Connected,
    int Answered,
    int Waiting);

public enum GameOperationKind
{
    Pause,
    Resume,
    Cancel,
    ForceFinish
}

public sealed record GameOperation(
    Guid GameId,
    GameOperationKind Kind,
    string RowVersion,
    string IdempotencyKey,
    string? Reason,
    string ActorId,
    DateTimeOffset Timestamp,
    string CorrelationId);

public sealed record GameAuditEntry(
    Guid GameId,
    string ActorId,
    DateTimeOffset Timestamp,
    GameStateView FromState,
    GameStateView ToState,
    string Action,
    string? Reason,
    string CorrelationId,
    string Result,
    string IdempotencyKey,
    bool Privileged);
