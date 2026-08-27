using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.Services;
using OroQuizClash.Domain.Questions.ValueObjects;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Application.Features.Games;

public sealed record StartRoundCommand(Guid GameId) : ICommand<Result<GameRoundResponse>>;

public sealed record GameRoundResponse(Guid Id, Guid GameId, int RoundNumber, Guid QuestionId, string Status, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt);

public sealed class StartRoundHandler(
    IRepository<Game, GameId> repository,
    IQuestionSelectionStrategy selector,
    IUnitOfWork unitOfWork) : ICommandHandler<StartRoundCommand, Result<GameRoundResponse>>
{
    public async Task<Result<GameRoundResponse>> HandleAsync(StartRoundCommand command, CancellationToken ct)
    {
        var game = await repository.GetByIdAsync(new GameId(command.GameId), ct);
        if (game is null) return Result.Failure<GameRoundResponse>(GameErrors.GameNotFound);

        // Build criteria from game config + previous questions
        var previous = game.Rounds.Select(r => r.QuestionId).ToList();
        var criteria = new QuestionSelectionCriteria(
            game.Configuration.CategoryId,
            null, // difficulty progressive could be added later
            null,
            null,
            previous,
            game.Id.Value,
            game.Rounds.Count + 1,
            null,
            1);

        var selection = await selector.SelectAsync(criteria, ct);
        if (selection.IsFailure) return Result.Failure<GameRoundResponse>(selection.Error);
        var question = selection.Value.FirstOrDefault();
        if (question is null) return Result.Failure<GameRoundResponse>(GameErrors.NoAvailableQuestion);

        var result = game.StartRound(question.Id.Value);
        if (result.IsFailure) return Result.Failure<GameRoundResponse>(result.Error);

        var round = result.Value;
        repository.Update(game);
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Failure<GameRoundResponse>(GameErrors.ConcurrencyConflict); }

        return Result.Success(new GameRoundResponse(round.Id.Value, round.GameId.Value, round.RoundNumber, round.QuestionId.Value, round.Status.Name, round.StartedAt, round.CompletedAt));
    }
}

public sealed class StartRoundEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/games/{id:guid}/rounds/start", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new StartRoundCommand(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}
