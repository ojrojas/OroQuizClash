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

public sealed record GetPlayerParticipationStatusQuery(Guid GameId, Guid PlayerId) : IQuery<Result<ParticipationStatusResponse>>;

public sealed record ParticipationStatusResponse(
    Guid GameId,
    Guid PlayerId,
    string ParticipationStatus,
    int CurrentPoints,
    int SecuredPoints,
    DateTimeOffset? ExitedAt);

public sealed class GetPlayerParticipationStatusHandler(IRepository<Game, GameId> repository) : IQueryHandler<GetPlayerParticipationStatusQuery, Result<ParticipationStatusResponse>>
{
    public async Task<Result<ParticipationStatusResponse>> HandleAsync(GetPlayerParticipationStatusQuery query, CancellationToken ct)
    {
        var spec = new GameByIdWithAnswersSpecification(new GameId(query.GameId));
        var game = await repository.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<ParticipationStatusResponse>(GameErrors.GameNotFound);

        var player = game.Players.FirstOrDefault(p => p.UserId == query.PlayerId);
        if (player is null) return Result.Failure<ParticipationStatusResponse>(GameErrors.PlayerNotInGame);

        return Result.Success(new ParticipationStatusResponse(
            query.GameId,
            query.PlayerId,
            player.ParticipationStatus.Name,
            player.Score.CurrentPoints,
            player.Score.SecuredPoints,
            player.ExitedAt));
    }
}

public sealed class GetPlayerParticipationStatusEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/games/{id:guid}/players/{playerId:guid}/status", async (
            Guid id,
            Guid playerId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetPlayerParticipationStatusQuery(id, playerId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
