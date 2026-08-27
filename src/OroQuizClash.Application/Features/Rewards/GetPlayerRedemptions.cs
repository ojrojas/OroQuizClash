using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Rewards;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Rewards;

public sealed record GetPlayerRedemptionsQuery(Guid PlayerId) : IQuery<Result<GetPlayerRedemptionsResponse>>;

public sealed record RedemptionItemResponse(
    Guid Id,
    Guid RewardId,
    Guid GameId,
    int Points,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? DeliveredAt);

public sealed record GetPlayerRedemptionsResponse(IReadOnlyCollection<RedemptionItemResponse> Redemptions);

public sealed class GetPlayerRedemptionsHandler(
    IRepository<RewardRedemption, RewardRedemptionId> redemptionRepo) : IQueryHandler<GetPlayerRedemptionsQuery, Result<GetPlayerRedemptionsResponse>>
{
    public async Task<Result<GetPlayerRedemptionsResponse>> HandleAsync(GetPlayerRedemptionsQuery query, CancellationToken ct)
    {
        var spec = new RedemptionsByPlayerSpecification(query.PlayerId);
        var redemptions = await redemptionRepo.ListAsync(spec, ct);

        var items = redemptions.Select(r => new RedemptionItemResponse(
            r.Id.Value, r.RewardId.Value, r.GameId.Value,
            r.Points, r.Status.Name, r.RequestedAt, r.DeliveredAt)).ToList();

        return Result.Success(new GetPlayerRedemptionsResponse(items));
    }
}

public sealed class GetPlayerRedemptionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/redemptions", async (
            ISender sender,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var playerId = Guid.TryParse(
                httpContext.User.FindFirst("sub")?.Value, out var sub) ? sub : Guid.Empty;
            var result = await sender.SendAsync(new GetPlayerRedemptionsQuery(playerId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
