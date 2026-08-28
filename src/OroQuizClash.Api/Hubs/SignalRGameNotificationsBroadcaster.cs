using Microsoft.AspNetCore.SignalR;

using OroQuizClash.Application.Features.Games;
using OroQuizClash.Application.Features.Games.Notifications;

namespace OroQuizClash.Api.Hubs;

/// <summary>
/// SignalR adapter for the notifications port. Payloads follow
/// specs/012-realtime-game-events/contracts/gamehub.md (camelCase via default hub protocol).
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

    public Task GameStartedAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(GameHub.GroupName(gameId))
            .SendAsync("GameStarted", new { gameId }, cancellationToken);

    public Task RoundStartedAsync(Guid gameId, Guid roundId, int roundNumber, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(GameHub.GroupName(gameId))
            .SendAsync("RoundStarted", new { gameId, roundId, roundNumber }, cancellationToken);

    public Task QuestionPresentedAsync(Guid gameId, Guid roundId, int roundNumber, QuestionPresentedPayload payload, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(GameHub.GroupName(gameId))
            .SendAsync("QuestionPresented", new { gameId, roundId, roundNumber, question = payload }, cancellationToken);

    public Task PlayerAnsweredAsync(Guid gameId, Guid playerId, Guid roundId, DateTimeOffset answeredAt, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(GameHub.GroupName(gameId))
            .SendAsync("PlayerAnswered", new { gameId, playerId, roundId, answeredAt }, cancellationToken);

    public Task RoundCompletedAsync(Guid gameId, Guid roundId, int roundNumber, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(GameHub.GroupName(gameId))
            .SendAsync("RoundCompleted", new { gameId, roundId, roundNumber }, cancellationToken);

    public Task GameFinishedAsync(Guid gameId, string status, IReadOnlyList<LeaderboardEntryResponse> entries, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(GameHub.GroupName(gameId))
            .SendAsync("GameFinished", new { gameId, status, players = entries }, cancellationToken);
}
