using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Rewards;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Rewards;

public sealed record RedeemRewardCommand(
    Guid RewardId,
    Guid GameId,
    Guid PlayerId,
    Guid? IdempotencyKey) : ICommand<Result<RedeemRewardResponse>>;

public sealed record RedeemRewardResponse(
    Guid RedemptionId,
    Guid RewardId,
    Guid GameId,
    int Points,
    string Status,
    DateTimeOffset RequestedAt);

public sealed class RedeemRewardValidator : IValidator<RedeemRewardCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(RedeemRewardCommand request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.RewardId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.RewardId), "RewardId required."));
        if (request.GameId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.GameId), "GameId required."));
        if (request.PlayerId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.PlayerId), "PlayerId required."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class RedeemRewardHandler(
    IRepository<Reward, RewardId> rewardRepo,
    IRepository<Game, GameId> gameRepo,
    IRepository<RewardRedemption, RewardRedemptionId> redemptionRepo,
    IUnitOfWork unitOfWork) : ICommandHandler<RedeemRewardCommand, Result<RedeemRewardResponse>>
{
    public async Task<Result<RedeemRewardResponse>> HandleAsync(RedeemRewardCommand command, CancellationToken ct)
    {
        if (command.IdempotencyKey.HasValue)
        {
            var existingSpec = new RedemptionByIdempotencyKeySpecification(command.PlayerId, command.IdempotencyKey.Value);
            var existing = await redemptionRepo.FirstOrDefaultAsync(existingSpec, ct);
            if (existing is not null)
            {
                return Result.Success(new RedeemRewardResponse(
                    existing.Id.Value,
                    existing.RewardId.Value,
                    existing.GameId.Value,
                    existing.Points,
                    existing.Status.Name,
                    existing.RequestedAt));
            }
        }

        var rewardSpec = new RewardByIdSpecification(new RewardId(command.RewardId));
        var reward = await rewardRepo.FirstOrDefaultAsync(rewardSpec, ct);
        if (reward is null) return Result.Failure<RedeemRewardResponse>(RewardErrors.RewardNotFound);

        var now = DateTimeOffset.UtcNow;
        var reserveResult = reward.ReserveStock(now);
        if (reserveResult.IsFailure)
            return Result.Failure<RedeemRewardResponse>(reserveResult.Error);

        var gameSpec = new GameByIdWithAnswersSpecification(new GameId(command.GameId));
        var game = await gameRepo.FirstOrDefaultAsync(gameSpec, ct);
        if (game is null) return Result.Failure<RedeemRewardResponse>(RewardErrors.RewardNotFound);

        var consumeResult = game.ConsumePoints(
            command.PlayerId,
            reward.PointsRequired,
            $"Redemption {command.RewardId}");
        if (consumeResult.IsFailure)
        {
            reward.ReleaseStock();
            return Result.Failure<RedeemRewardResponse>(consumeResult.Error);
        }

        var createResult = RewardRedemption.Create(
            command.PlayerId,
            reward.Id,
            game.Id,
            reward.PointsRequired,
            command.IdempotencyKey);
        if (createResult.IsFailure)
        {
            reward.ReleaseStock();
            return Result.Failure<RedeemRewardResponse>(createResult.Error);
        }

        var redemption = createResult.Value;
        await redemptionRepo.AddAsync(redemption, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new RedeemRewardResponse(
            redemption.Id.Value,
            redemption.RewardId.Value,
            redemption.GameId.Value,
            redemption.Points,
            redemption.Status.Name,
            redemption.RequestedAt));
    }
}

public sealed class RedeemRewardEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/rewards/{rewardId:guid}/redeem", async (
            Guid rewardId,
            RedeemRewardRequest body,
            ISender sender,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var playerId = Guid.TryParse(
                httpContext.User.FindFirst("sub")?.Value, out var sub) ? sub : Guid.Empty;
            var command = new RedeemRewardCommand(rewardId, body.GameId, playerId, body.IdempotencyKey);
            var result = await sender.SendAsync(command, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record RedeemRewardRequest(Guid GameId, Guid? IdempotencyKey);
