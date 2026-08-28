using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;

using Microsoft.Extensions.Logging;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Events;

namespace OroQuizClash.Application.Features.Games.Notifications;

/// <summary>
/// Broadcasts game lifecycle events (SPEC-012 US3).
/// </summary>
public sealed class GameStartedBroadcastHandler(
    IGameNotificationsBroadcaster broadcaster,
    ILogger<GameStartedBroadcastHandler> logger) : IDomainEventHandler<GameStartedDomainEvent>
{
    public async Task HandleAsync(GameStartedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            await broadcaster.GameStartedAsync(domainEvent.GameId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast GameStarted for game {GameId}", domainEvent.GameId);
        }
    }
}

public sealed class GameFinishedBroadcastHandler(
    IGameNotificationsBroadcaster broadcaster,
    IRepository<Game, GameId> gameRepository,
    ILogger<GameFinishedBroadcastHandler> logger)
    : IDomainEventHandler<GameFinishedDomainEvent>,
      IDomainEventHandler<GameForcedFinishedDomainEvent>,
      IDomainEventHandler<GameCancelledDomainEvent>
{
    public Task HandleAsync(GameFinishedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        BroadcastAsync(domainEvent.GameId, "FINISHED", cancellationToken);

    public Task HandleAsync(GameForcedFinishedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        BroadcastAsync(domainEvent.GameId, "FORCED_FINISHED", cancellationToken);

    public Task HandleAsync(GameCancelledDomainEvent domainEvent, CancellationToken cancellationToken) =>
        BroadcastAsync(domainEvent.GameId, "CANCELLED", cancellationToken);

    private async Task BroadcastAsync(Guid gameId, string status, CancellationToken cancellationToken)
    {
        try
        {
            var game = await BroadcastGameLoader.LoadGameAsync(gameRepository, gameId, cancellationToken);
            if (game is null) return;
            var entries = LeaderboardBuilder.Build(game);
            await broadcaster.GameFinishedAsync(gameId, status, entries, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast GameFinished for game {GameId}", gameId);
        }
    }
}
