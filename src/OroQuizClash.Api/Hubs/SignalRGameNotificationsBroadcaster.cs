using Microsoft.AspNetCore.SignalR;

using OroQuizClash.Application.Features.Games;

namespace OroQuizClash.Api.Hubs;

/// <summary>
/// SignalR adapter for the notifications port. Payloads follow
/// specs/011-multiplayer/contracts/gamehub.md (camelCase via default hub protocol).
/// </summary>
public sealed class SignalRGameNotificationsBroadcaster(IHubContext<GameHub> hubContext) : IGameNotificationsBroadcaster
{
    public Task PlayerJoinedAsync(Guid gameId, Guid playerId, string? displayName, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(GameHub.GroupName(gameId))
            .SendAsync("PlayerJoined", new { gameId, playerId, displayName }, cancellationToken);

    public Task ScoreUpdatedAsync(Guid gameId, Guid playerId, int points, int totalPoints, string reason, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(GameHub.GroupName(gameId))
            .SendAsync("ScoreUpdated", new { gameId, playerId, points, totalPoints, reason }, cancellationToken);

    public Task LeaderboardUpdatedAsync(Guid gameId, IReadOnlyList<LeaderboardEntryResponse> entries, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(GameHub.GroupName(gameId))
            .SendAsync("LeaderboardUpdated", new { gameId, players = entries }, cancellationToken);

    public Task PlayerStatusChangedAsync(Guid gameId, Guid playerId, string status, int? finalScore, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(GameHub.GroupName(gameId))
            .SendAsync("PlayerStatusChanged", new { gameId, playerId, status, finalScore }, cancellationToken);
}
