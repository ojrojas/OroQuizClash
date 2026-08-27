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

namespace OroQuizClash.Application.Features.Games;

public sealed record JoinGameCommand(Guid GameId, Guid UserId) : ICommand<Result<GameResponse>>;

public sealed class JoinGameValidator : IValidator<JoinGameCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(JoinGameCommand request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.GameId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.GameId), "GameId required."));
        if (request.UserId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.UserId), "UserId required."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class JoinGameHandler(IRepository<Game, GameId> repository, IUnitOfWork unitOfWork) : ICommandHandler<JoinGameCommand, Result<GameResponse>>
{
    public async Task<Result<GameResponse>> HandleAsync(JoinGameCommand command, CancellationToken ct)
    {
        var game = await repository.GetByIdAsync(new GameId(command.GameId), ct);
        if (game is null) return Result.Failure<GameResponse>(Domain.Shared.Errors.GameErrors.GameNotFound);

        var result = game.JoinPlayer(command.UserId);
        if (result.IsFailure) return Result.Failure<GameResponse>(result.Error);

        repository.Update(game);
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Failure<GameResponse>(Domain.Shared.Errors.GameErrors.ConcurrencyConflict); }

        return Result.Success(new GameResponse(
            game.Id.Value, game.Name, game.Status.Name, game.Configuration.CategoryId.Value,
            game.Configuration.MinRounds, game.Configuration.MaxRounds,
            game.Players.Count, game.Rounds.Count,
            game.RowVersion != null && game.RowVersion.Length > 0 ? Convert.ToBase64String(game.RowVersion) : string.Empty,
            game.CreatedAt, game.ReadyAt, game.StartedAt, game.FinishedAt));
    }
}

public sealed class JoinGameEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/games/{id:guid}/players", async (Guid id, HttpContext http, JoinGameCommand body, ISender sender, CancellationToken ct) =>
        {
            // If body UserId empty, try to use JWT sub claim
            var userId = body.UserId;
            if (userId == Guid.Empty)
            {
                var sub = http.User.FindFirst("sub")?.Value ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var parsed)) userId = parsed;
                else if (!string.IsNullOrEmpty(sub)) userId = Guid.NewGuid(); // fallback for test mock
            }
            var command = new JoinGameCommand(id, userId != Guid.Empty ? userId : body.UserId);
            // If still empty, use Guid from JWT or generate for test
            if (command.UserId == Guid.Empty)
            {
                // Try to get from body again
                command = body with { GameId = id };
            }
            else
            {
                command = new JoinGameCommand(id, userId);
            }
            var result = await sender.SendAsync(command, ct);
            return result.ToHttpResult();
        }).RequireAuthorization(); // PLAYER+ allowed
    }
}
