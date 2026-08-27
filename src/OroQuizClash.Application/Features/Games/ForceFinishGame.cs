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

namespace OroQuizClash.Application.Features.Games;

public sealed record ForceFinishGameCommand(Guid GameId, string Reason) : ICommand<Result<GameResponse>>;

public sealed class ForceFinishGameValidator : IValidator<ForceFinishGameCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(ForceFinishGameCommand request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.GameId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.GameId), "GameId required."));
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3 || request.Reason.Trim().Length > 500)
            failures.Add(new ValidationFailure(nameof(request.Reason), "Reason must be 3-500 characters."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class ForceFinishGameHandler(IRepository<Game, GameId> repository, IUnitOfWork unitOfWork) : ICommandHandler<ForceFinishGameCommand, Result<GameResponse>>
{
    public async Task<Result<GameResponse>> HandleAsync(ForceFinishGameCommand command, CancellationToken ct)
    {
        var game = await repository.GetByIdAsync(new GameId(command.GameId), ct);
        if (game is null) return Result.Failure<GameResponse>(GameErrors.GameNotFound);
        var result = game.ForceFinish(command.Reason);
        if (result.IsFailure) return Result.Failure<GameResponse>(result.Error);
        repository.Update(game);
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Failure<GameResponse>(GameErrors.ConcurrencyConflict); }
        return Result.Success(new GameResponse(
            game.Id.Value, game.Name, game.Status.Name, game.Configuration.CategoryId.Value,
            game.Configuration.MinRounds, game.Configuration.MaxRounds,
            game.Players.Count, game.Rounds.Count,
            game.RowVersion != null && game.RowVersion.Length > 0 ? Convert.ToBase64String(game.RowVersion) : string.Empty,
            game.CreatedAt, game.ReadyAt, game.StartedAt, game.FinishedAt));
    }
}

public sealed class ForceFinishGameEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/games/{id:guid}/force-finish", async (Guid id, ForceFinishGameCommand body, ISender sender, CancellationToken ct) =>
        {
            var command = new ForceFinishGameCommand(id, body.Reason);
            var result = await sender.SendAsync(command, ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}
