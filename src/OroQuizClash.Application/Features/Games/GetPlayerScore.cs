using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Shared.Errors;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games;

public sealed record GetPlayerScoreQuery(Guid GameId, Guid PlayerId) : IQuery<Result<ScoreResponse>>;

public sealed record ScoreResponse(
    Guid GameId,
    Guid PlayerId,
    int CurrentPoints,
    int SecuredPoints,
    int RoundPoints,
    int PotentialPoints,
    int TotalPoints,
    int CorrectAnswers,
    int IncorrectAnswers,
    int TotalAnswered,
    bool IsWithdrawn);

public sealed class GetPlayerScoreHandler(IRepository<Game, GameId> repository) : IQueryHandler<GetPlayerScoreQuery, Result<ScoreResponse>>
{
    public async Task<Result<ScoreResponse>> HandleAsync(GetPlayerScoreQuery query, CancellationToken ct)
    {
        var spec = new GameByIdWithAnswersSpecification(new GameId(query.GameId));
        var game = await repository.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<ScoreResponse>(GameErrors.GameNotFound);

        var player = game.Players.FirstOrDefault(p => p.UserId == query.PlayerId);
        if (player is null) return Result.Failure<ScoreResponse>(GameErrors.PlayerNotInGame);

        var transactions = game.PointTransactions
            .Where(pt => pt.PlayerId == query.PlayerId)
            .ToList();

        var correctAnswers = transactions.Count(pt => pt.Type == PointTransactionType.AnswerCorrect);
        var incorrectAnswers = transactions.Count(pt => pt.Type == PointTransactionType.AnswerIncorrect);
        var score = player.Score;

        return Result.Success(new ScoreResponse(
            query.GameId,
            query.PlayerId,
            score.CurrentPoints,
            score.SecuredPoints,
            score.RoundPoints,
            score.PotentialPoints,
            score.TotalPoints,
            correctAnswers,
            incorrectAnswers,
            correctAnswers + incorrectAnswers,
            player.IsWithdrawn));
    }
}

public sealed class GetPlayerScoreEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/games/{id:guid}/score/{playerId:guid}", async (
            Guid id,
            Guid playerId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetPlayerScoreQuery(id, playerId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
