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

public sealed record GetRedemptionsQuery(RedemptionStatus? Status) : IQuery<Result<GetRedemptionsResponse>>;

public sealed record RedemptionDetailResponse(
    Guid Id,
    Guid PlayerId,
    Guid RewardId,
    Guid GameId,
    int Points,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? DeliveredAt,
    IReadOnlyCollection<RedemptionTransitionResponse> Transitions);

public sealed record RedemptionTransitionResponse(string Status, Guid ActorId, DateTimeOffset At);

public sealed record GetRedemptionsResponse(IReadOnlyCollection<RedemptionDetailResponse> Redemptions);

public sealed class GetRedemptionsHandler(
    IRepository<RewardRedemption, RewardRedemptionId> redemptionRepo) : IQueryHandler<GetRedemptionsQuery, Result<GetRedemptionsResponse>>
{
    public async Task<Result<GetRedemptionsResponse>> HandleAsync(GetRedemptionsQuery query, CancellationToken ct)
    {
        var spec = new RedemptionsByStatusSpecification(query.Status);
        var redemptions = await redemptionRepo.ListAsync(spec, ct);

        var items = redemptions.Select(r => new RedemptionDetailResponse(
            r.Id.Value, r.PlayerId, r.RewardId.Value, r.GameId.Value,
            r.Points, r.Status.Name, r.RequestedAt, r.DeliveredAt,
            r.Transitions.Select(t => new RedemptionTransitionResponse(
                t.Status.Name, t.ActorId, t.At)).ToList())).ToList();

        return Result.Success(new GetRedemptionsResponse(items));
    }
}

public sealed class GetRedemptionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/redemptions/all", async (
            string? status,
            ISender sender,
            CancellationToken ct) =>
        {
            RedemptionStatus? filterStatus = null;
            if (!string.IsNullOrEmpty(status))
                filterStatus = RedemptionStatus.FromName(status);

            var result = await sender.SendAsync(new GetRedemptionsQuery(filterStatus), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrRewardManager");
    }
}
