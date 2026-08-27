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

namespace OroQuizClash.Application.Features.Rewards;

public sealed record UpdateRewardCommand(
    Guid RewardId,
    string? Name,
    string? Description,
    int? PointsRequired,
    int? Stock,
    DateTimeOffset? ExpirationDate) : ICommand<Result<UpdateRewardResponse>>;

public sealed record UpdateRewardResponse(
    Guid Id,
    string Name,
    string Description,
    int PointsRequired,
    int Stock,
    string Status,
    DateTimeOffset? ExpirationDate);

public sealed class UpdateRewardValidator : IValidator<UpdateRewardCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(UpdateRewardCommand request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.RewardId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.RewardId), "RewardId required."));
        if (request.Name is not null && (request.Name.Trim().Length is < 3 or > 100))
            failures.Add(new ValidationFailure(nameof(request.Name), "Name must be 3–100 characters."));
        if (request.PointsRequired is not null && request.PointsRequired <= 0)
            failures.Add(new ValidationFailure(nameof(request.PointsRequired), "Points required must be > 0."));
        if (request.Stock is not null && request.Stock < 0)
            failures.Add(new ValidationFailure(nameof(request.Stock), "Stock must not be negative."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class UpdateRewardHandler(
    IRepository<Reward, RewardId> rewardRepo,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateRewardCommand, Result<UpdateRewardResponse>>
{
    public async Task<Result<UpdateRewardResponse>> HandleAsync(UpdateRewardCommand command, CancellationToken ct)
    {
        var spec = new RewardByIdSpecification(new RewardId(command.RewardId));
        var reward = await rewardRepo.FirstOrDefaultAsync(spec, ct);
        if (reward is null) return Result.Failure<UpdateRewardResponse>(RewardErrors.RewardNotFound);

        var result = reward.Update(command.Name, command.Description, command.PointsRequired, command.Stock, command.ExpirationDate);
        if (result.IsFailure) return Result.Failure<UpdateRewardResponse>(result.Error);

        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new UpdateRewardResponse(
            reward.Id.Value, reward.Name, reward.Description,
            reward.PointsRequired, reward.Stock, reward.Status.Name, reward.ExpirationDate));
    }
}

public sealed class UpdateRewardEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/rewards/{rewardId:guid}", async (
            Guid rewardId,
            UpdateRewardRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new UpdateRewardCommand(rewardId, body.Name, body.Description, body.PointsRequired, body.Stock, body.ExpirationDate);
            var result = await sender.SendAsync(command, ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrRewardManager");
    }
}

public sealed record UpdateRewardRequest(
    string? Name,
    string? Description,
    int? PointsRequired,
    int? Stock,
    DateTimeOffset? ExpirationDate);
