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

public sealed record GetPlayerConsolationStatusQuery(Guid GameId, Guid PlayerId) : IQuery<Result<ConsolationStatusResponse>>;

public sealed record ConsolationStatusResponse(
    Guid GameId,
    Guid PlayerId,
    bool Received,
    string Policy,
    int? Points,
    string? RewardName,
    DateTimeOffset? Timestamp);

public sealed class GetPlayerConsolationStatusHandler(
    IRepository<Game, GameId> gameRepo) : IQueryHandler<GetPlayerConsolationStatusQuery, Result<ConsolationStatusResponse>>
{
    public async Task<Result<ConsolationStatusResponse>> HandleAsync(GetPlayerConsolationStatusQuery query, CancellationToken ct)
    {
        var spec = new GameByIdWithAnswersSpecification(new GameId(query.GameId));
        var game = await gameRepo.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<ConsolationStatusResponse>(GameErrors.GameNotFound);

        var player = game.Players.FirstOrDefault(p => p.UserId == query.PlayerId);
        if (player is null) return Result.Failure<ConsolationStatusResponse>(GameErrors.PlayerNotInGame);

        var consolidation = game.PointTransactions
            .FirstOrDefault(pt => pt.Type == PointTransactionType.Consolation && pt.PlayerId == query.PlayerId);

        if (consolidation is null)
        {
            return Result.Success(new ConsolationStatusResponse(
                query.GameId, query.PlayerId, false,
                game.Configuration.ConsolationPolicy.Name, null, null, null));
        }

        return Result.Success(new ConsolationStatusResponse(
            query.GameId, query.PlayerId, true,
            game.Configuration.ConsolationPolicy.Name,
            consolidation.Points, null, consolidation.CreatedAt));
    }
}

public sealed class GetPlayerConsolationStatusEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/games/{gameId:guid}/players/{playerId:guid}/consolation", async (
            Guid gameId,
            Guid playerId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetPlayerConsolationStatusQuery(gameId, playerId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
