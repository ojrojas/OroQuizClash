using OroQuizClash.Application.Features.Games.Notifications;

namespace OroQuizClash.Application.Features.Games;

/// <summary>
/// Notifications port for realtime game state hints (SPEC-011 FR-014, SPEC-012 FR-001).
/// Broadcast-only: the hub is never the source of truth; clients re-query REST
/// endpoints for authoritative state. Implementations must be best-effort.
/// </summary>
public interface IGameNotificationsBroadcaster
{
    Task PlayerJoinedAsync(Guid gameId, Guid playerId, string? displayName, CancellationToken cancellationToken = default);

    Task ScoreUpdatedAsync(Guid gameId, Guid playerId, int points, int totalPoints, string reason, CancellationToken cancellationToken = default);

    Task LeaderboardUpdatedAsync(Guid gameId, IReadOnlyList<LeaderboardEntryResponse> entries, CancellationToken cancellationToken = default);

    Task PlayerStatusChangedAsync(Guid gameId, Guid playerId, string status, int? finalScore, CancellationToken cancellationToken = default);

    Task GameStartedAsync(Guid gameId, CancellationToken cancellationToken = default);

    Task RoundStartedAsync(Guid gameId, Guid roundId, int roundNumber, CancellationToken cancellationToken = default);

    Task QuestionPresentedAsync(Guid gameId, Guid roundId, int roundNumber, QuestionPresentedPayload payload, CancellationToken cancellationToken = default);

    Task PlayerAnsweredAsync(Guid gameId, Guid playerId, Guid roundId, DateTimeOffset answeredAt, CancellationToken cancellationToken = default);

    Task RoundCompletedAsync(Guid gameId, Guid roundId, int roundNumber, CancellationToken cancellationToken = default);

    Task GameFinishedAsync(Guid gameId, string status, IReadOnlyList<LeaderboardEntryResponse> entries, CancellationToken cancellationToken = default);
}
