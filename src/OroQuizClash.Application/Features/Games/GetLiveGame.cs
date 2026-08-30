using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.Services;
using OroQuizClash.Domain.Shared.Errors;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games;

public sealed record GetLiveGameQuery(Guid GameId) : IQuery<Result<LiveGameResponse>>;

public sealed record LiveGameResponse(
    Guid GameId,
    string Status,
    int CurrentRound,
    Guid? CurrentRoundId,
    LiveQuestionResponse? CurrentQuestion,
    int TotalRounds,
    int Players,
    int PlayersConnected,
    int PlayersAnswered,
    int PlayersWaiting,
    IReadOnlyList<LiveScoreResponse> Scores,
    int CurrentLevel,
    int RemainingSeconds,
    string RowVersion,
    DateTimeOffset LastUpdated);

public sealed record LiveQuestionResponse(
    Guid QuestionId,
    string Text,
    IReadOnlyList<LiveAnswerOptionResponse> Options,
    string? CorrectAnswer);

public sealed record LiveAnswerOptionResponse(Guid OptionId, string Text, char Position);

public sealed record LiveScoreResponse(
    Guid PlayerId,
    string DisplayName,
    int Score,
    int SecuredPoints,
    int Level,
    bool HasAnswered);

public sealed class GetLiveGameHandler(
    IRepository<Game, GameId> gameRepository,
    IRepository<Question, QuestionId> questionRepository) : IQueryHandler<GetLiveGameQuery, Result<LiveGameResponse>>
{
    public async Task<Result<LiveGameResponse>> HandleAsync(GetLiveGameQuery query, CancellationToken ct)
    {
        var spec = new GameByIdWithAnswersSpecification(new GameId(query.GameId));
        var game = await gameRepository.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<LiveGameResponse>(GameErrors.GameNotFound);

        var currentRound = game.Rounds
            .OrderByDescending(r => r.RoundNumber)
            .FirstOrDefault(r => r.Status.Name is "ROUND_IN_PROGRESS" or "RoundInProgress");

        LiveQuestionResponse? question = null;
        if (currentRound is not null)
        {
            var q = await questionRepository.FirstOrDefaultAsync(
                new QuestionByIdSpecification(currentRound.QuestionId), ct);
            if (q is not null)
            {
                var positions = new[] { 'A', 'B', 'C', 'D' };
                var options = q.AnswerOptions
                    .OrderBy(o => o.DisplayOrder)
                    .Select((o, i) => new LiveAnswerOptionResponse(o.Id.Value, o.Text, i < positions.Length ? positions[i] : '?'))
                    .ToList();
                question = new LiveQuestionResponse(q.Id.Value, q.Text, options, null);
            }
        }

        var leaderboard = LeaderboardBuilder.Build(game);
        var scores = leaderboard.Select(e => new LiveScoreResponse(
            e.PlayerId,
            e.DisplayName ?? $"Player {e.PlayerId.ToString()[..8]}",
            e.Points,
            e.SecuredPoints,
            e.CurrentLevel ?? 0,
            false)).ToList();

        var answeredCount = currentRound is not null
            ? game.Answers.Count(a => a.RoundId == currentRound.Id && a.Status.Name is "Answered" or "Evaluated")
            : 0;

        var totalPlayers = game.Players.Count(p => p.ParticipationStatus.Name is "Active");
        var remainingSeconds = currentRound is not null
            ? Math.Max(0, (int)(currentRound.StartedAt.AddSeconds(currentRound.TimeLimit) - DateTimeOffset.UtcNow).TotalSeconds)
            : 0;

        var currentLevel = currentRound?.Difficulty ?? 0;

        var gameState = game.Status.Name.ToUpperInvariant() switch
        {
            "DRAFT" => "Draft",
            "CONFIGURED" => "Configured",
            "SCHEDULED" => "Scheduled",
            "READY" => "Ready",
            "WAITING_FOR_PLAYERS" => "Scheduled",
            "IN_PROGRESS" => "Running",
            "ROUND_IN_PROGRESS" => "Running",
            "ROUND_COMPLETED" => "Running",
            "PAUSED" => "Paused",
            "FINISHED" => "Finished",
            "FORCED_FINISHED" => "Finished",
            "CANCELLED" => "Cancelled",
            _ => "Draft"
        };

        return Result.Success(new LiveGameResponse(
            game.Id.Value,
            gameState,
            currentRound?.RoundNumber ?? 0,
            currentRound?.Id.Value,
            question,
            game.Configuration.MaxRounds,
            totalPlayers,
            0,
            answeredCount,
            totalPlayers - answeredCount,
            scores,
            currentLevel,
            remainingSeconds,
            game.RowVersion != null && game.RowVersion.Length > 0 ? Convert.ToBase64String(game.RowVersion) : string.Empty,
            DateTimeOffset.UtcNow));
    }
}

public sealed class GetLiveGameEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/games/{id:guid}/live", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetLiveGameQuery(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}
