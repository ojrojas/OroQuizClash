using Microsoft.AspNetCore.SignalR.Client;

namespace QuizArena.Admin.Client.Services;

/// <summary>
/// WASM Live Games: connects to the SAME-ORIGIN forwarded hub /hubs/game (BFF). No
/// accessTokenFactory — the session cookie travels in the negotiate handshake and the
/// server-side forwarder attaches the operator's JWT (contracts/realtime.md §1).
/// </summary>
public sealed class ClientLiveGamesService(HttpClient httpClient)
    : LiveGamesServiceCore(httpClient, "bff")
{
    public override async Task<LiveGameSubscription> SubscribeAsync(Guid gameId, CancellationToken ct = default)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("/hubs/game")
            .WithAutomaticReconnect()
            .Build();
        var subscription = new SignalRLiveGameSubscription(connection, gameId);
        await subscription.StartAsync(ct);
        return subscription;
    }
}
