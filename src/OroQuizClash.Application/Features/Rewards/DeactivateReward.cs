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

public sealed record DeactivateRewardCommand(Guid RewardId) : ICommand<Result<DeactivateRewardResponse>>;

public sealed record DeactivateRewardResponse(Guid Id, string Name, string Status);

public sealed class DeactivateRewardHandler(
    IRepository<Reward, RewardId> rewardRepo,
    IUnitOfWork unitOfWork) : ICommandHandler<DeactivateRewardCommand, Result<DeactivateRewardResponse>>
{
    public async Task<Result<DeactivateRewardResponse>> HandleAsync(DeactivateRewardCommand command, CancellationToken ct)
    {
        var spec = new RewardByIdSpecification(new RewardId(command.RewardId));
        var reward = await rewardRepo.FirstOrDefaultAsync(spec, ct);
        if (reward is null) return Result.Failure<DeactivateRewardResponse>(RewardErrors.RewardNotFound);

        var result = reward.Deactivate();
        if (result.IsFailure) return Result.Failure<DeactivateRewardResponse>(result.Error);

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new DeactivateRewardResponse(reward.Id.Value, reward.Name, reward.Status.Name));
    }
}

public sealed class DeactivateRewardEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/rewards/{rewardId:guid}/deactivate", async (
            Guid rewardId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new DeactivateRewardCommand(rewardId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrRewardManager");
    }
}
