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

public sealed record ApproveRedemptionCommand(Guid RedemptionId, Guid ManagerId) : ICommand<Result<ApproveRedemptionResponse>>;

public sealed record ApproveRedemptionResponse(Guid RedemptionId, string Status, DateTimeOffset? DeliveredAt);

public sealed class ApproveRedemptionHandler(
    IRepository<RewardRedemption, RewardRedemptionId> redemptionRepo,
    IUnitOfWork unitOfWork) : ICommandHandler<ApproveRedemptionCommand, Result<ApproveRedemptionResponse>>
{
    public async Task<Result<ApproveRedemptionResponse>> HandleAsync(ApproveRedemptionCommand command, CancellationToken ct)
    {
        var spec = new RedemptionByIdSpecification(new RewardRedemptionId(command.RedemptionId));
        var redemption = await redemptionRepo.FirstOrDefaultAsync(spec, ct);
        if (redemption is null) return Result.Failure<ApproveRedemptionResponse>(RewardErrors.RedemptionNotFound);

        var result = redemption.Approve(command.ManagerId);
        if (result.IsFailure) return Result.Failure<ApproveRedemptionResponse>(result.Error);

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new ApproveRedemptionResponse(redemption.Id.Value, redemption.Status.Name, redemption.DeliveredAt));
    }
}

public sealed class ApproveRedemptionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/redemptions/{redemptionId:guid}/approve", async (
            Guid redemptionId,
            ISender sender,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var managerId = Guid.TryParse(
                httpContext.User.FindFirst("sub")?.Value, out var sub) ? sub : Guid.Empty;
            var result = await sender.SendAsync(new ApproveRedemptionCommand(redemptionId, managerId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrRewardManager");
    }
}
