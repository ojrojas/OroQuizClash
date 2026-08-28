using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Shared.Errors;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games;

public sealed record GetPlayerStateQuery(Guid GameId, Guid PlayerId) : IQuery<Result<PlayerStateResponse>>;

public sealed record PlayerStateResponse(
    Guid GameId,
    Guid PlayerId,
    string? DisplayName,
    string Status,
    int CurrentPoints,
    int SecuredPoints,
    int RoundPoints,
    int PotentialPoints,
    int TotalPoints,
    int CurrentRound,
    string AnswerState,
    int CorrectAnswers,
    int IncorrectAnswers,
    DateTimeOffset? ExitedAt);

public sealed class GetPlayerStateHandler(IRepository<Game, GameId> repository) : IQueryHandler<GetPlayerStateQuery, Result<PlayerStateResponse>>
{
    public async Task<Result<PlayerStateResponse>> HandleAsync(GetPlayerStateQuery query, CancellationToken ct)
    {
        var spec = new GameByIdWithAnswersSpecification(new GameId(query.GameId));
        var game = await repository.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<PlayerStateResponse>(GameErrors.GameNotFound);

        var player = game.Players.FirstOrDefault(p => p.UserId == query.PlayerId);
        if (player is null) return Result.Failure<PlayerStateResponse>(GameErrors.PlayerNotInGame);

        var answers = game.Answers.Where(a => a.PlayerId == query.PlayerId).ToList();
        var score = player.Score;

        return Result.Success(new PlayerStateResponse(
            query.GameId,
            query.PlayerId,
            player.DisplayName,
            player.ParticipationStatus.Name,
            score.CurrentPoints,
            score.SecuredPoints,
            score.RoundPoints,
            score.PotentialPoints,
            score.TotalPoints,
            player.CurrentRoundNumber,
            game.GetPlayerAnswerState(query.PlayerId).Name,
            answers.Count(a => a.Correct == true),
            answers.Count(a => a.Correct == false),
            player.ExitedAt));
    }
}

public sealed class GetPlayerStateEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/games/{gameId:guid}/players/{playerId:guid}/state", async (
            Guid gameId,
            Guid playerId,
            HttpContext http,
            ISender sender,
            CancellationToken ct) =>
        {
            var subId = GameClaims.GetSub(http.User);
            if (!GameClaims.IsOrganizer(http.User) && subId != Guid.Empty && subId != playerId)
                return Result.Failure(GameErrors.PlayerIdentityMismatch).ToHttpResult();

            var result = await sender.SendAsync(new GetPlayerStateQuery(gameId, playerId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
