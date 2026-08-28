namespace OroQuizClash.Application.Features.Games;

/// <summary>
/// Notifications port for multiplayer game state hints (SPEC-011 FR-014).
/// Broadcast-only: the hub is never the source of truth; clients re-query REST
/// endpoints for authoritative state. Implementations must be best-effort.
/// </summary>
public interface IGameNotificationsBroadcaster
{
    Task PlayerJoinedAsync(Guid gameId, Guid playerId, string? displayName, CancellationToken cancellationToken = default);

    Task ScoreUpdatedAsync(Guid gameId, Guid playerId, int points, int totalPoints, string reason, CancellationToken cancellationToken = default);

    Task LeaderboardUpdatedAsync(Guid gameId, IReadOnlyList<LeaderboardEntryResponse> entries, CancellationToken cancellationToken = default);

    Task PlayerStatusChangedAsync(Guid gameId, Guid playerId, string status, int? finalScore, CancellationToken cancellationToken = default);
}
