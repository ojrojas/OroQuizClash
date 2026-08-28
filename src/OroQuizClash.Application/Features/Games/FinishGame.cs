using BuildingBlocks.CQRS.Abstractions;
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

public sealed record FinishGameCommand(Guid GameId) : ICommand<Result<GameResponse>>;

public sealed class FinishGameHandler(IRepository<Game, GameId> repository, IUnitOfWork unitOfWork) : ICommandHandler<FinishGameCommand, Result<GameResponse>>
{
    public async Task<Result<GameResponse>> HandleAsync(FinishGameCommand command, CancellationToken ct)
    {
        var game = await repository.FirstOrDefaultAsync(new GameByIdWithAnswersSpecification(new GameId(command.GameId)), ct);
        if (game is null) return Result.Failure<GameResponse>(GameErrors.GameNotFound);
        var result = game.Finish();
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

public sealed class FinishGameEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/games/{id:guid}/finish", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new FinishGameCommand(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}
