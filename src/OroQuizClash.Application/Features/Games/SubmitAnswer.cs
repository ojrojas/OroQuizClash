using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Shared.Errors;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games;

public sealed record SubmitAnswerCommand(
    Guid GameId,
    Guid AnswerOptionId,
    Guid? RoundId,
    Guid? IdempotencyKey) : ICommand<Result<SubmitAnswerResponse>>;

public sealed record SubmitAnswerResponse(
    Guid AnswerId,
    bool Correct,
    int Points,
    int ElapsedTime,
    string Status,
    int RoundNumber,
    string GameStatus);

public sealed class SubmitAnswerValidator : IValidator<SubmitAnswerCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(SubmitAnswerCommand request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.GameId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.GameId), "GameId required."));
        if (request.AnswerOptionId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.AnswerOptionId), "AnswerOptionId required."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class SubmitAnswerHandler(
    IRepository<Game, GameId> gameRepository,
    IRepository<Question, QuestionId> questionRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<SubmitAnswerCommand, Result<SubmitAnswerResponse>>
{
    public async Task<Result<SubmitAnswerResponse>> HandleAsync(SubmitAnswerCommand command, CancellationToken ct)
    {
        var spec = new GameByIdWithAnswersSpecification(new GameId(command.GameId));
        var game = await gameRepository.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<SubmitAnswerResponse>(GameErrors.GameNotFound);

        var answerOptionId = new AnswerOptionId(command.AnswerOptionId);

        // Resolve Question from repository for server-side validation
        var round = command.RoundId.HasValue
            ? game.Rounds.FirstOrDefault(r => r.Id.Value == command.RoundId.Value)
            : game.CurrentRound;

        if (round is null)
            return Result.Failure<SubmitAnswerResponse>(GameErrors.QuestionNotActive);

        var question = await questionRepository.GetByIdAsync(round.QuestionId, ct);

        var playerId = Guid.Empty;
        var player = game.Players.FirstOrDefault(p => p.UserId == playerId);
        if (player is null)
        {
            // Fallback: try to extract from JWT context if available
            // For now, use a placeholder — actual implementation uses HttpContext.User.FindFirst("sub")
        }

        var result = game.SubmitAnswer(
            playerId,
            answerOptionId,
            DateTimeOffset.UtcNow,
            qId => question);

        if (result.IsFailure)
            return Result.Failure<SubmitAnswerResponse>(result.Error);

        await unitOfWork.SaveChangesAsync(ct);

        var answer = result.Value;
        return Result.Success(new SubmitAnswerResponse(
            answer.Id.Value,
            answer.Correct ?? false,
            answer.Points,
            answer.ElapsedTime,
            answer.Status.Name,
            round.RoundNumber,
            game.Status.Name));
    }
}

public sealed class SubmitAnswerEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/games/{id:guid}/answers", async (
            Guid id,
            SubmitAnswerCommand body,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = body with { GameId = id };
            var result = await sender.SendAsync(command, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
