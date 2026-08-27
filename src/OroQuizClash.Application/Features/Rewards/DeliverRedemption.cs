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

public sealed record DeliverRedemptionCommand(Guid RedemptionId, Guid ManagerId) : ICommand<Result<DeliverRedemptionResponse>>;

public sealed record DeliverRedemptionResponse(Guid RedemptionId, string Status, DateTimeOffset? DeliveredAt);

public sealed class DeliverRedemptionHandler(
    IRepository<RewardRedemption, RewardRedemptionId> redemptionRepo,
    IUnitOfWork unitOfWork) : ICommandHandler<DeliverRedemptionCommand, Result<DeliverRedemptionResponse>>
{
    public async Task<Result<DeliverRedemptionResponse>> HandleAsync(DeliverRedemptionCommand command, CancellationToken ct)
    {
        var spec = new RedemptionByIdSpecification(new RewardRedemptionId(command.RedemptionId));
        var redemption = await redemptionRepo.FirstOrDefaultAsync(spec, ct);
        if (redemption is null) return Result.Failure<DeliverRedemptionResponse>(RewardErrors.RedemptionNotFound);

        var result = redemption.Deliver(command.ManagerId);
        if (result.IsFailure) return Result.Failure<DeliverRedemptionResponse>(result.Error);

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new DeliverRedemptionResponse(redemption.Id.Value, redemption.Status.Name, redemption.DeliveredAt));
    }
}

public sealed class DeliverRedemptionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/redemptions/{redemptionId:guid}/deliver", async (
            Guid redemptionId,
            ISender sender,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var managerId = Guid.TryParse(
                httpContext.User.FindFirst("sub")?.Value, out var sub) ? sub : Guid.Empty;
            var result = await sender.SendAsync(new DeliverRedemptionCommand(redemptionId, managerId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrRewardManager");
    }
}
