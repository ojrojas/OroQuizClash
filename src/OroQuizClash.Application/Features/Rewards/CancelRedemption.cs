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

public sealed record CancelRedemptionCommand(Guid RedemptionId, Guid PlayerId) : ICommand<Result<CancelRedemptionResponse>>;

public sealed record CancelRedemptionResponse(Guid RedemptionId, string Status);

public sealed class CancelRedemptionHandler(
    IRepository<RewardRedemption, RewardRedemptionId> redemptionRepo,
    IRepository<Reward, RewardId> rewardRepo,
    IRepository<Game, GameId> gameRepo,
    IUnitOfWork unitOfWork) : ICommandHandler<CancelRedemptionCommand, Result<CancelRedemptionResponse>>
{
    public async Task<Result<CancelRedemptionResponse>> HandleAsync(CancelRedemptionCommand command, CancellationToken ct)
    {
        var spec = new RedemptionByIdSpecification(new RewardRedemptionId(command.RedemptionId));
        var redemption = await redemptionRepo.FirstOrDefaultAsync(spec, ct);
        if (redemption is null) return Result.Failure<CancelRedemptionResponse>(RewardErrors.RedemptionNotFound);

        var cancelResult = redemption.Cancel(command.PlayerId);
        if (cancelResult.IsFailure) return Result.Failure<CancelRedemptionResponse>(cancelResult.Error);

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
                $"Refund for redemption {redemption.Id.Value} (CANCELLED)");
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new CancelRedemptionResponse(redemption.Id.Value, redemption.Status.Name));
    }
}

public sealed class CancelRedemptionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/redemptions/{redemptionId:guid}/cancel", async (
            Guid redemptionId,
            ISender sender,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var playerId = Guid.TryParse(
                httpContext.User.FindFirst("sub")?.Value, out var sub) ? sub : Guid.Empty;
            var result = await sender.SendAsync(new CancelRedemptionCommand(redemptionId, playerId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
