using BuildingBlocks.CQRS.Abstractions;

using Microsoft.Extensions.Logging;

using OroQuizClash.Domain.Games.Events;

namespace OroQuizClash.Application.Features.Games.Notifications;

/// <summary>
/// Broadcasts PlayerAnswered (SPEC-012 US2) — without AnswerOptionId/correct/points.
/// </summary>
public sealed class PlayerAnsweredBroadcastHandler(
    IGameNotificationsBroadcaster broadcaster,
    ILogger<PlayerAnsweredBroadcastHandler> logger) : IDomainEventHandler<AnswerSubmittedDomainEvent>
{
    public async Task HandleAsync(AnswerSubmittedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            await broadcaster.PlayerAnsweredAsync(
                domainEvent.GameId,
                domainEvent.PlayerId,
                domainEvent.RoundId,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast PlayerAnswered for game {GameId} player {PlayerId}", domainEvent.GameId, domainEvent.PlayerId);
        }
    }
}
