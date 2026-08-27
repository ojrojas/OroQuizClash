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

public sealed record ActivateRewardCommand(Guid RewardId) : ICommand<Result<ActivateRewardResponse>>;

public sealed record ActivateRewardResponse(Guid Id, string Name, string Status);

public sealed class ActivateRewardHandler(
    IRepository<Reward, RewardId> rewardRepo,
    IUnitOfWork unitOfWork) : ICommandHandler<ActivateRewardCommand, Result<ActivateRewardResponse>>
{
    public async Task<Result<ActivateRewardResponse>> HandleAsync(ActivateRewardCommand command, CancellationToken ct)
    {
        var spec = new RewardByIdSpecification(new RewardId(command.RewardId));
        var reward = await rewardRepo.FirstOrDefaultAsync(spec, ct);
        if (reward is null) return Result.Failure<ActivateRewardResponse>(RewardErrors.RewardNotFound);

        var result = reward.Activate();
        if (result.IsFailure) return Result.Failure<ActivateRewardResponse>(result.Error);

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new ActivateRewardResponse(reward.Id.Value, reward.Name, reward.Status.Name));
    }
}

public sealed class ActivateRewardEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/rewards/{rewardId:guid}/activate", async (
            Guid rewardId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new ActivateRewardCommand(rewardId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrRewardManager");
    }
}
