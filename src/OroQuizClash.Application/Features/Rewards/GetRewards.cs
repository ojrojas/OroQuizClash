using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Rewards;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Rewards;

public sealed record GetRewardsQuery(Guid? GameId, bool IncludeUnavailable) : IQuery<Result<GetRewardsResponse>>;

public sealed record RewardItemResponse(
    Guid Id,
    string Name,
    string Description,
    int PointsRequired,
    int Stock,
    string Status,
    DateTimeOffset? ExpirationDate,
    bool Available);

public sealed record GetRewardsResponse(
    IReadOnlyCollection<RewardItemResponse> Rewards,
    int? AvailablePoints,
    Guid? GameId);

public sealed class GetRewardsHandler(
    IRepository<Reward, RewardId> rewardRepo,
    IRepository<Game, GameId> gameRepo) : IQueryHandler<GetRewardsQuery, Result<GetRewardsResponse>>
{
    public async Task<Result<GetRewardsResponse>> HandleAsync(GetRewardsQuery query, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        IReadOnlyCollection<Reward> rewards;
        if (query.IncludeUnavailable)
        {
            var spec = new AllRewardsSpecification();
            rewards = await rewardRepo.ListAsync(spec, ct);
        }
        else
        {
            var spec = new AvailableRewardsSpecification(now);
            rewards = await rewardRepo.ListAsync(spec, ct);
        }

        var items = rewards.Select(r => new RewardItemResponse(
            r.Id.Value,
            r.Name,
            r.Description,
            r.PointsRequired,
            r.Stock,
            r.Status.Name,
            r.ExpirationDate,
            r.IsAvailable(now))).ToList();

        int? availablePoints = null;
        if (query.GameId is not null)
        {
            var gameSpec = new GameByIdWithAnswersSpecification(new GameId(query.GameId.Value));
            var game = await gameRepo.FirstOrDefaultAsync(gameSpec, ct);
            if (game is not null)
            {
                var player = game.Players.FirstOrDefault(p => p.UserId == /* current user sub */ Guid.Empty);
                if (player is not null)
                    availablePoints = player.Score.CurrentPoints;
            }
        }

        return Result.Success(new GetRewardsResponse(items, availablePoints, query.GameId));
    }
}

public sealed class GetRewardsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/rewards", async (
            Guid? gameId,
            bool includeUnavailable,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetRewardsQuery(gameId, includeUnavailable), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
