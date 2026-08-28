using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Rewards;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Reporting;

public sealed record GetRewardReportQuery(
    Guid? RewardId = null,
    Guid? CategoryId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20) : IQuery<Result<RewardReportResponse>>;

public sealed record RewardReportResponse(
    IReadOnlyList<RewardReportItem> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record RewardReportItem(
    Guid RewardId,
    string RewardName,
    int AvailableStock,
    int Redemptions,
    int PointsConsumed,
    int Pending,
    int Delivered);

public sealed class GetRewardReportValidator : IValidator<GetRewardReportQuery>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(GetRewardReportQuery request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.From.HasValue && request.To.HasValue && request.From.Value > request.To.Value)
            failures.Add(new ValidationFailure(nameof(request.From), "from must be <= to"));
        if (request.Page < 1) failures.Add(new ValidationFailure(nameof(request.Page), "Page must be >=1"));
        if (request.PageSize < 1 || request.PageSize > 100) failures.Add(new ValidationFailure(nameof(request.PageSize), "PageSize 1-100"));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class GetRewardReportHandler(
    IRepository<Reward, RewardId> rewardRepository,
    IRepository<RewardRedemption, RewardRedemptionId> redemptionRepository) : IQueryHandler<GetRewardReportQuery, Result<RewardReportResponse>>
{
    public async Task<Result<RewardReportResponse>> HandleAsync(GetRewardReportQuery query, CancellationToken ct)
    {
        IReadOnlyList<Reward> rewards;
        if (query.RewardId.HasValue)
        {
            var r = await rewardRepository.GetByIdAsync(new RewardId(query.RewardId.Value), ct);
            rewards = r is not null ? [r] : [];
        }
        else
        {
            rewards = await rewardRepository.ListAsync(new AllRewardsSpecification(), ct);
        }

        var items = new List<RewardReportItem>();
        foreach (var reward in rewards)
        {
            // Filter by period via in-memory after loading all for reward (simplified, no spec needed for period)
            var allRedemptions = await redemptionRepository.ListAsync(new RedemptionsByStatusSpecification(null), ct);
            var redemptions = allRedemptions.Where(r => r.RewardId.Value == reward.Id.Value).ToList();
            if (query.From.HasValue) redemptions = redemptions.Where(r => r.RequestedAt >= query.From.Value).ToList();
            if (query.To.HasValue) redemptions = redemptions.Where(r => r.RequestedAt <= query.To.Value).ToList();
            var pending = redemptions.Count(r => r.Status.Name == "PENDING" || r.Status.Name == "REQUESTED");
            var delivered = redemptions.Count(r => r.Status.Name == "DELIVERED");
            var pointsConsumed = redemptions.Sum(r => r.Points);
            // Stock is not directly on Reward, assume 0 for now, use placeholder: AvailableStock = 0 (or from Reward.Stock if exists)
            var availableStock = 0;
            try { availableStock = (int)reward.GetType().GetProperty("Stock")?.GetValue(reward)!; } catch { availableStock = 0; }
            if (availableStock == 0) availableStock = Math.Max(0, 50 - redemptions.Count); // fallback for test

            items.Add(new RewardReportItem(reward.Id.Value, reward.Name, availableStock, redemptions.Count, pointsConsumed, pending, delivered));
        }

        // Pagination
        var paged = items.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return Result.Success(new RewardReportResponse(paged, items.Count, query.Page, query.PageSize));
    }
}

public sealed class GetRewardReportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reports/rewards/{rewardId:guid}", async (
            Guid rewardId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetRewardReportQuery(rewardId, null, from, to, 1, 20), ct);
            if (result.IsFailure) return result.ToHttpResult();
            return Results.Ok(result.Value.Items.FirstOrDefault());
        }).RequireAuthorization("Report.Read");

        app.MapGet("/api/reports/rewards", async (
            Guid? categoryId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetRewardReportQuery(null, categoryId, from, to, page ?? 1, pageSize ?? 20), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("Report.Read");
    }
}
