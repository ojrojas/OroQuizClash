using Microsoft.AspNetCore.SignalR.Client;
using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

/// <summary>
/// SignalR-backed <see cref="LiveGameSubscription"/>. Subscribes ONLY to aggregate events
/// (GameStarted, PlayerJoined, RoundStarted, RoundCompleted, GameFinished, LeaderboardUpdated);
/// private events (QuestionPresented, PlayerAnswered, ScoreUpdated) get no handler (SPEC-016 §11).
/// Server Truth: after a reconnection the REST snapshot must be re-queried before live data
/// is shown again (contracts/realtime.md §3) — signalled via ResyncRequested.
/// </summary>
public sealed class SignalRLiveGameSubscription : LiveGameSubscription
{
    private readonly HubConnection connection;
    private LiveConnectionView state = LiveConnectionView.Disconnected;

    public SignalRLiveGameSubscription(HubConnection connection, Guid gameId)
    {
        this.connection = connection;
        GameId = gameId;

        foreach (var eventName in AdminEvents)
        {
            connection.On<object?>(eventName, _ => RaiseLiveEventReceived(eventName));
        }

        connection.Reconnecting += _ =>
        {
            SetState(LiveConnectionView.Reconnecting);
            return Task.CompletedTask;
        };
        connection.Reconnected += async _ =>
        {
            SetState(LiveConnectionView.Connected);
            // Server Truth: re-query REST before trusting live events again.
            await RaiseResyncRequestedAsync();
        };
        connection.Closed += _ =>
        {
            SetState(LiveConnectionView.Disconnected);
            return Task.CompletedTask;
        };
    }

    public override Guid GameId { get; }

    public override LiveConnectionView ConnectionState => state;

    public async Task StartAsync(CancellationToken ct = default)
    {
        await connection.StartAsync(ct);
        SetState(LiveConnectionView.Connected);
        await connection.InvokeAsync("JoinGameGroup", GameId, ct);
    }

    private void SetState(LiveConnectionView newState)
    {
        if (state == newState)
        {
            return;
        }
        state = newState;
        RaiseConnectionStateChanged(newState);
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await connection.DisposeAsync();
        }
        finally
        {
            SetState(LiveConnectionView.Disconnected);
        }
    }
}
