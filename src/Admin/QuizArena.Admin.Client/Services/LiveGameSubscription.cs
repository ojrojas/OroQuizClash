using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

/// <summary>
/// Live subscription to a single game group via the forwarded GameHub (/hubs/game).
/// Events are best-effort notifications; the REST API remains the source of truth
/// (Constitution V — Server Truth). After a reconnection the implementation MUST
/// raise <see cref="ResyncRequested"/> so the UI re-queries REST before showing
/// live data again. Privacy: QuestionPresented/PlayerAnswered/ScoreUpdated are
/// ignored (SPEC-016 §11 — aggregates only).
/// </summary>
public abstract class LiveGameSubscription : IAsyncDisposable
{
    public static readonly string[] AdminEvents =
    [
        "GameStarted",
        "PlayerJoined",
        "RoundStarted",
        "RoundCompleted",
        "GameFinished",
        "LeaderboardUpdated"
    ];

    public static readonly string[] IgnoredPrivateEvents =
    [
        "QuestionPresented",
        "PlayerAnswered",
        "ScoreUpdated"
    ];

    public abstract Guid GameId { get; }
    public abstract LiveConnectionView ConnectionState { get; }

    public event Action<LiveConnectionView>? ConnectionStateChanged;
    public event Action<string>? LiveEventReceived;
    public event Func<Task>? ResyncRequested;

    public static bool IsAdminEvent(string eventName) =>
        AdminEvents.Contains(eventName, StringComparer.Ordinal);

    protected void RaiseConnectionStateChanged(LiveConnectionView state) =>
        ConnectionStateChanged?.Invoke(state);

    protected void RaiseLiveEventReceived(string eventName) =>
        LiveEventReceived?.Invoke(eventName);

    protected Task RaiseResyncRequestedAsync() =>
        ResyncRequested?.Invoke() ?? Task.CompletedTask;

    public abstract ValueTask DisposeAsync();
}
