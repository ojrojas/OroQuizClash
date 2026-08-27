using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games;

public sealed record GetPlayerConsolationHistoryQuery(Guid PlayerId) : IQuery<Result<ConsolationHistoryResponse>>;

public sealed record ConsolationHistoryItem(
    Guid GameId,
    string GameName,
    string Policy,
    int? Points,
    string? RewardName,
    DateTimeOffset Timestamp);

public sealed record ConsolationHistoryResponse(
    Guid PlayerId,
    IReadOnlyCollection<ConsolationHistoryItem> Consolations);

public sealed class GetPlayerConsolationHistoryHandler(
    IRepository<Game, GameId> gameRepo) : IQueryHandler<GetPlayerConsolationHistoryQuery, Result<ConsolationHistoryResponse>>
{
    public async Task<Result<ConsolationHistoryResponse>> HandleAsync(GetPlayerConsolationHistoryQuery query, CancellationToken ct)
    {
        var spec = new AllGamesWithPlayerSpecification(query.PlayerId);
        var games = await gameRepo.ListAsync(spec, ct);

        var consolations = new List<ConsolationHistoryItem>();

        foreach (var game in games)
        {
            var consolidation = game.PointTransactions
                .FirstOrDefault(pt => pt.Type == PointTransactionType.Consolation && pt.PlayerId == query.PlayerId);

            if (consolidation is not null)
            {
                consolations.Add(new ConsolationHistoryItem(
                    game.Id.Value,
                    game.Configuration.Name,
                    game.Configuration.ConsolationPolicy.Name,
                    consolidation.Points,
                    null,
                    consolidation.CreatedAt));
            }
        }

        return Result.Success(new ConsolationHistoryResponse(query.PlayerId, consolations));
    }
}

public sealed class GetPlayerConsolationHistoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/players/{playerId:guid}/consolation-history", async (
            Guid playerId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetPlayerConsolationHistoryQuery(playerId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
