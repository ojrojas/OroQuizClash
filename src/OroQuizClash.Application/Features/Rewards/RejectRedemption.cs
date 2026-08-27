using BuildingBlocks.CQRS.Abstractions;
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

public sealed record RejectRedemptionCommand(Guid RedemptionId, Guid ManagerId) : ICommand<Result<RejectRedemptionResponse>>;

public sealed record RejectRedemptionResponse(Guid RedemptionId, string Status);

public sealed class RejectRedemptionHandler(
    IRepository<RewardRedemption, RewardRedemptionId> redemptionRepo,
    IRepository<Reward, RewardId> rewardRepo,
    IRepository<Game, GameId> gameRepo,
    IUnitOfWork unitOfWork) : ICommandHandler<RejectRedemptionCommand, Result<RejectRedemptionResponse>>
{
    public async Task<Result<RejectRedemptionResponse>> HandleAsync(RejectRedemptionCommand command, CancellationToken ct)
    {
        var spec = new RedemptionByIdSpecification(new RewardRedemptionId(command.RedemptionId));
        var redemption = await redemptionRepo.FirstOrDefaultAsync(spec, ct);
        if (redemption is null) return Result.Failure<RejectRedemptionResponse>(RewardErrors.RedemptionNotFound);

        var rejectResult = redemption.Reject(command.ManagerId);
        if (rejectResult.IsFailure) return Result.Failure<RejectRedemptionResponse>(rejectResult.Error);

        var rewardSpec = new RewardByIdSpecification(redemption.RewardId);
        var reward = await rewardRepo.FirstOrDefaultAsync(rewardSpec, ct);
        reward?.ReleaseStock();

        var gameSpec = new GameByIdWithAnswersSpecification(redemption.GameId);
        var game = await gameRepo.FirstOrDefaultAsync(gameSpec, ct);
        if (game is not null)
        {
            game.RefundPoints(
                redemption.PlayerId,
                redemption.Points,
                $"Refund for redemption {redemption.Id.Value} (REJECTED)");
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new RejectRedemptionResponse(redemption.Id.Value, redemption.Status.Name));
    }
}

public sealed class RejectRedemptionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/redemptions/{redemptionId:guid}/reject", async (
            Guid redemptionId,
            ISender sender,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var managerId = Guid.TryParse(
                httpContext.User.FindFirst("sub")?.Value, out var sub) ? sub : Guid.Empty;
            var result = await sender.SendAsync(new RejectRedemptionCommand(redemptionId, managerId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrRewardManager");
    }
}
