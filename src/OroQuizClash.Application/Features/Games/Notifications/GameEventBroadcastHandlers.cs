using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;

using Microsoft.Extensions.Logging;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Events;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games.Notifications;

/// <summary>
/// Maps game domain events to best-effort SignalR hints (SPEC-011 FR-014).
/// Handlers run inside the SaveChanges transaction (pre-commit); broadcast
/// failures are logged and swallowed so they never break the transaction.
/// The hub is never the source of truth — clients re-query REST on doubt.
/// </summary>
public sealed class PlayerJoinedBroadcastHandler(
    IGameNotificationsBroadcaster broadcaster,
    IRepository<Game, GameId> gameRepository,
    ILogger<PlayerJoinedBroadcastHandler> logger) : IDomainEventHandler<PlayerJoinedDomainEvent>
{
    public async Task HandleAsync(PlayerJoinedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            var game = await BroadcastGameLoader.LoadGameAsync(gameRepository, domainEvent.GameId, cancellationToken);
            var displayName = game?.Players.FirstOrDefault(p => p.UserId == domainEvent.UserId)?.DisplayName;
            await broadcaster.PlayerJoinedAsync(domainEvent.GameId, domainEvent.UserId, displayName, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast PlayerJoined for game {GameId}", domainEvent.GameId);
        }
    }
}

public sealed class ScoreUpdatedBroadcastHandler(
    IGameNotificationsBroadcaster broadcaster,
    ILogger<ScoreUpdatedBroadcastHandler> logger) : IDomainEventHandler<ScoreUpdatedDomainEvent>
{
    public async Task HandleAsync(ScoreUpdatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            await broadcaster.ScoreUpdatedAsync(
                domainEvent.GameId,
                domainEvent.PlayerId,
                domainEvent.Points,
                domainEvent.ResultingBalance,
                domainEvent.Type,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast ScoreUpdated for game {GameId}", domainEvent.GameId);
        }
    }
}

public sealed class LeaderboardBroadcastHandler(
    IGameNotificationsBroadcaster broadcaster,
    IRepository<Game, GameId> gameRepository,
    ILogger<LeaderboardBroadcastHandler> logger)
    : IDomainEventHandler<AnswerEvaluatedDomainEvent>, IDomainEventHandler<RoundCompletedDomainEvent>
{
    public Task HandleAsync(AnswerEvaluatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        BroadcastLeaderboardAsync(domainEvent.GameId, cancellationToken);

    public Task HandleAsync(RoundCompletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        BroadcastLeaderboardAsync(domainEvent.GameId, cancellationToken);

    private async Task BroadcastLeaderboardAsync(Guid gameId, CancellationToken cancellationToken)
    {
        try
        {
            var game = await BroadcastGameLoader.LoadGameAsync(gameRepository, gameId, cancellationToken);
            if (game is null) return;
            await broadcaster.LeaderboardUpdatedAsync(gameId, LeaderboardBuilder.Build(game), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast LeaderboardUpdated for game {GameId}", gameId);
        }
    }
}

public sealed class PlayerStatusBroadcastHandler(
    IGameNotificationsBroadcaster broadcaster,
    IRepository<Game, GameId> gameRepository,
    ILogger<PlayerStatusBroadcastHandler> logger)
    : IDomainEventHandler<PlayerWithdrawnDomainEvent>,
      IDomainEventHandler<PlayerEliminatedDomainEvent>,
      IDomainEventHandler<GameFinishedDomainEvent>
{
    public async Task HandleAsync(PlayerWithdrawnDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            await broadcaster.PlayerStatusChangedAsync(
                domainEvent.GameId, domainEvent.PlayerId, "WITHDRAWN", domainEvent.RetainedPoints, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast PlayerStatusChanged for game {GameId}", domainEvent.GameId);
        }
    }

    public async Task HandleAsync(PlayerEliminatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            var game = await BroadcastGameLoader.LoadGameAsync(gameRepository, domainEvent.GameId, cancellationToken);
            var finalScore = game?.Players.FirstOrDefault(p => p.UserId == domainEvent.PlayerId)?.Score.CurrentPoints;
            await broadcaster.PlayerStatusChangedAsync(
                domainEvent.GameId, domainEvent.PlayerId, "ELIMINATED", finalScore, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast PlayerStatusChanged for game {GameId}", domainEvent.GameId);
        }
    }

    public async Task HandleAsync(GameFinishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            var game = await BroadcastGameLoader.LoadGameAsync(gameRepository, domainEvent.GameId, cancellationToken);
            if (game is null) return;

            foreach (var entry in LeaderboardBuilder.Build(game))
            {
                var status = entry.Rank == 1 ? "WINNER" : "FINISHED";
                await broadcaster.PlayerStatusChangedAsync(
                    domainEvent.GameId, entry.PlayerId, status, entry.Points, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast PlayerStatusChanged for game {GameId}", domainEvent.GameId);
        }
    }
}

file static class BroadcastGameLoader
{
    public static Task<Game?> LoadGameAsync(
        IRepository<Game, GameId> repository, Guid gameId, CancellationToken cancellationToken) =>
        repository.FirstOrDefaultAsync(new GameByIdWithAnswersSpecification(new GameId(gameId)), cancellationToken);
}
