using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Application.Features.Games;

public sealed record StartGameCommand(Guid GameId) : ICommand<Result>;

public sealed class StartGameHandler(IRepository<Game, GameId> games, IUnitOfWork unitOfWork)
    : ICommandHandler<StartGameCommand, Result>
{
    public async Task<Result> HandleAsync(StartGameCommand command, CancellationToken ct)
    {
        var game = await games.GetByIdAsync(new GameId(command.GameId), ct);
        if (game is null) return Result.Failure(GameErrors.GameNotFound);
        var result = game.Start();
        if (result.IsFailure) return result;
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class StartGameEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/games/{gameId:guid}/start", async (Guid gameId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new StartGameCommand(gameId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}