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

public sealed record WithdrawPlayerCommand(Guid GameId, Guid PlayerId) : ICommand<Result<WithdrawPlayerResponse>>;

public sealed record WithdrawPlayerResponse(
    Guid GameId,
    Guid PlayerId,
    string ParticipationStatus,
    int PointsDeducted,
    int FinalScore,
    string WithdrawalPolicy,
    DateTimeOffset? WithdrawnAt);

public sealed class WithdrawPlayerValidator : IValidator<WithdrawPlayerCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(WithdrawPlayerCommand request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.GameId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.GameId), "GameId required."));
        if (request.PlayerId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.PlayerId), "PlayerId required."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class WithdrawPlayerHandler(
    IRepository<Game, GameId> gameRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<WithdrawPlayerCommand, Result<WithdrawPlayerResponse>>
{
    public async Task<Result<WithdrawPlayerResponse>> HandleAsync(WithdrawPlayerCommand command, CancellationToken ct)
    {
        var spec = new GameByIdWithAnswersSpecification(new GameId(command.GameId));
        var game = await gameRepository.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<WithdrawPlayerResponse>(GameErrors.GameNotFound);

        var result = game.WithdrawPlayer(command.PlayerId);
        if (result.IsFailure)
            return Result.Failure<WithdrawPlayerResponse>(result.Error);

        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Failure<WithdrawPlayerResponse>(GameErrors.ConcurrencyConflict); }

        var score = game.GetPlayerScore(command.PlayerId);
        var player = game.Players.First(p => p.UserId == command.PlayerId);
        return Result.Success(new WithdrawPlayerResponse(
            command.GameId,
            command.PlayerId,
            player.ParticipationStatus.Name,
            -result.Value.Points,
            score.CurrentPoints,
            game.Configuration.WithdrawalPolicy.Name,
            player.ExitedAt));
    }
}

public sealed class WithdrawPlayerEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/games/{id:guid}/withdraw", async (
            Guid id,
            HttpContext http,
            WithdrawPlayerRequest? body,
            ISender sender,
            CancellationToken ct) =>
        {
            var subId = GameClaims.GetSub(http.User);

            var playerId = body?.PlayerId ?? Guid.Empty;
            if (playerId == Guid.Empty) playerId = subId;

            // Players may only withdraw themselves; organizers may withdraw any player (SPEC-011 FR-003).
            if (playerId != subId && !GameClaims.IsOrganizer(http.User))
                return Result.Failure(GameErrors.PlayerIdentityMismatch).ToHttpResult();

            var command = new WithdrawPlayerCommand(id, playerId);
            var result = await sender.SendAsync(command, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record WithdrawPlayerRequest(Guid PlayerId);
