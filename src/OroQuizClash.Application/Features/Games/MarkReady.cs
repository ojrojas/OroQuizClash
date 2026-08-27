using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Questions.Services;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Application.Features.Games;

public sealed record MarkReadyCommand(Guid GameId) : ICommand<Result<GameResponse>>;

public sealed class MarkReadyHandler(
    IRepository<Game, GameId> repository,
    IRepository<Category, CategoryId> categoryRepository,
    OroQuizClash.Domain.Questions.Services.IQuestionCounter questionCounter,
    IUnitOfWork unitOfWork) : ICommandHandler<MarkReadyCommand, Result<GameResponse>>
{
    public async Task<Result<GameResponse>> HandleAsync(MarkReadyCommand command, CancellationToken ct)
    {
        var game = await repository.GetByIdAsync(new GameId(command.GameId), ct);
        if (game is null) return Result.Failure<GameResponse>(GameErrors.GameNotFound);

        Func<Guid, bool> isPublished = id =>
        {
            // Check via repository: category exists and is ACTIVE (published)
            var cat = categoryRepository.GetByIdAsync(new CategoryId(id), ct).GetAwaiter().GetResult();
            return cat != null && cat.Status == CategoryStatus.Active;
        };

        Func<Guid, int> countValid = id =>
            questionCounter.CountValidAsync(new CategoryId(id), ct).GetAwaiter().GetResult();

        var result = game.MarkReady(isPublished, countValid);
        if (result.IsFailure) return Result.Failure<GameResponse>(result.Error);

        repository.Update(game);
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Failure<GameResponse>(GameErrors.ConcurrencyConflict); }

        return Result.Success(Map(game));
    }

    private static GameResponse Map(Game g) => new(
        g.Id.Value, g.Name, g.Status.Name, g.Configuration.CategoryId.Value,
        g.Configuration.MinRounds, g.Configuration.MaxRounds,
        g.Players.Count, g.Rounds.Count,
        g.RowVersion != null && g.RowVersion.Length > 0 ? Convert.ToBase64String(g.RowVersion) : string.Empty,
        g.CreatedAt, g.ReadyAt, g.StartedAt, g.FinishedAt);
}

public sealed record GameResponse(
    Guid Id, string Name, string Status, Guid CategoryId,
    int MinRounds, int MaxRounds,
    int PlayerCount, int RoundCount,
    string RowVersion,
    DateTimeOffset CreatedAt, DateTimeOffset? ReadyAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt);

public sealed class MarkReadyEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/games/{id:guid}/ready", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new MarkReadyCommand(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}
