using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Shared.Errors;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games;

public sealed record AdjustScoreCommand(
    Guid GameId,
    Guid PlayerId,
    int Points,
    string Reason,
    Guid AdminUserId) : ICommand<Result<AdjustScoreResponse>>;

public sealed record AdjustScoreResponse(
    Guid GameId,
    Guid PlayerId,
    Guid TransactionId,
    int Points,
    int ResultingBalance,
    string Reason,
    DateTimeOffset CreatedAt);

public sealed class AdjustScoreValidator : IValidator<AdjustScoreCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(AdjustScoreCommand request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.GameId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.GameId), "GameId required."));
        if (request.PlayerId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.PlayerId), "PlayerId required."));
        if (request.Points == 0) failures.Add(new ValidationFailure(nameof(request.Points), "Points must not be zero."));
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3 || request.Reason.Trim().Length > 500)
            failures.Add(new ValidationFailure(nameof(request.Reason), "Reason must be 3-500 characters."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class AdjustScoreHandler(
    IRepository<Game, GameId> gameRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<AdjustScoreCommand, Result<AdjustScoreResponse>>
{
    public async Task<Result<AdjustScoreResponse>> HandleAsync(AdjustScoreCommand command, CancellationToken ct)
    {
        var spec = new GameByIdWithAnswersSpecification(new GameId(command.GameId));
        var game = await gameRepository.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<AdjustScoreResponse>(GameErrors.GameNotFound);

        var result = game.AdjustPoints(command.PlayerId, command.Points, command.Reason, command.AdminUserId);
        if (result.IsFailure)
            return Result.Failure<AdjustScoreResponse>(result.Error);

        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Failure<AdjustScoreResponse>(GameErrors.ConcurrencyConflict); }

        var transaction = result.Value;
        return Result.Success(new AdjustScoreResponse(
            command.GameId,
            command.PlayerId,
            transaction.Id.Value,
            transaction.Points,
            transaction.ResultingBalance,
            transaction.Reason ?? command.Reason,
            transaction.CreatedAt));
    }
}

public sealed class AdjustScoreEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/games/{id:guid}/score/{playerId:guid}/adjust", async (
            Guid id,
            Guid playerId,
            AdjustScoreRequest body,
            ISender sender,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var adminUserId = Guid.TryParse(
                httpContext.User.FindFirst("sub")?.Value, out var sub) ? sub : Guid.Empty;
            var command = new AdjustScoreCommand(id, playerId, body.Points, body.Reason, adminUserId);
            var result = await sender.SendAsync(command, ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}

public sealed record AdjustScoreRequest(int Points, string Reason);
