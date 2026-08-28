using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;

using Microsoft.Extensions.Logging;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Events;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games.Notifications;

/// <summary>
/// Broadcasts round lifecycle events (SPEC-012 US1).
/// TODO (FR-012/R8): filter QuestionPresented/RoundStarted for withdrawn/eliminated
/// players via game-{gameId}-active sub-group if server-side filtering required.
/// Current v1 broadcasts to whole group; client ignores after WITHDRAWN.
/// </summary>
public sealed class RoundStartedBroadcastHandler(
    IGameNotificationsBroadcaster broadcaster,
    IRepository<Question, QuestionId> questionRepository,
    ILogger<RoundStartedBroadcastHandler> logger) : IDomainEventHandler<RoundStartedDomainEvent>
{
    public async Task HandleAsync(RoundStartedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            await broadcaster.RoundStartedAsync(domainEvent.GameId, domainEvent.RoundId, domainEvent.RoundNumber, cancellationToken);

            var question = await questionRepository.FirstOrDefaultAsync(
                new QuestionByIdSpecification(new QuestionId(domainEvent.QuestionId)), cancellationToken);
            if (question is null)
            {
                logger.LogWarning("Question {QuestionId} not found for RoundStarted broadcast game {GameId}", domainEvent.QuestionId, domainEvent.GameId);
                return;
            }

            var payload = new QuestionPresentedPayload(
                question.Id.Value,
                question.Text,
                question.AnswerOptions
                    .OrderBy(o => o.DisplayOrder)
                    .Select(o => new QuestionOptionPayload(o.Id.Value, o.Text))
                    .ToList());

            await broadcaster.QuestionPresentedAsync(domainEvent.GameId, domainEvent.RoundId, domainEvent.RoundNumber, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast RoundStarted/QuestionPresented for game {GameId} round {RoundId}", domainEvent.GameId, domainEvent.RoundId);
        }
    }
}

public sealed class RoundCompletedBroadcastHandler(
    IGameNotificationsBroadcaster broadcaster,
    IRepository<Game, GameId> gameRepository,
    ILogger<RoundCompletedBroadcastHandler> logger) : IDomainEventHandler<RoundCompletedDomainEvent>
{
    public async Task HandleAsync(RoundCompletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            var game = await BroadcastGameLoader.LoadGameAsync(gameRepository, domainEvent.GameId, cancellationToken);
            var roundNumber = game?.Rounds.FirstOrDefault(r => r.Id.Value == domainEvent.RoundId)?.RoundNumber ?? 0;
            await broadcaster.RoundCompletedAsync(domainEvent.GameId, domainEvent.RoundId, roundNumber, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast RoundCompleted for game {GameId} round {RoundId}", domainEvent.GameId, domainEvent.RoundId);
        }
    }
}
