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

public sealed record CreateRewardCommand(
    string Name,
    string Description,
    int PointsRequired,
    int Stock,
    DateTimeOffset? ExpirationDate) : ICommand<Result<CreateRewardResponse>>;

public sealed record CreateRewardResponse(
    Guid Id,
    string Name,
    string Description,
    int PointsRequired,
    int Stock,
    string Status,
    DateTimeOffset? ExpirationDate);

public sealed class CreateRewardValidator : IValidator<CreateRewardCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(CreateRewardCommand request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 3 or > 100)
            failures.Add(new ValidationFailure(nameof(request.Name), "Name must be 3–100 characters."));
        if (request.PointsRequired <= 0)
            failures.Add(new ValidationFailure(nameof(request.PointsRequired), "Points required must be > 0."));
        if (request.Stock < 0)
            failures.Add(new ValidationFailure(nameof(request.Stock), "Stock must not be negative."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class CreateRewardHandler(
    IRepository<Reward, RewardId> rewardRepo,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateRewardCommand, Result<CreateRewardResponse>>
{
    public async Task<Result<CreateRewardResponse>> HandleAsync(CreateRewardCommand command, CancellationToken ct)
    {
        var result = Reward.Create(command.Name, command.Description, command.PointsRequired, command.Stock, command.ExpirationDate);
        if (result.IsFailure) return Result.Failure<CreateRewardResponse>(result.Error);

        var reward = result.Value;
        await rewardRepo.AddAsync(reward, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new CreateRewardResponse(
            reward.Id.Value, reward.Name, reward.Description,
            reward.PointsRequired, reward.Stock, reward.Status.Name, reward.ExpirationDate));
    }
}

public sealed class CreateRewardEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/rewards", async (
            CreateRewardRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CreateRewardCommand(body.Name, body.Description, body.PointsRequired, body.Stock, body.ExpirationDate);
            var result = await sender.SendAsync(command, ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrRewardManager");
    }
}

public sealed record CreateRewardRequest(
    string Name,
    string Description,
    int PointsRequired,
    int Stock,
    DateTimeOffset? ExpirationDate);
