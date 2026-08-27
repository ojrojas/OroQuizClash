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

namespace OroQuizClash.Application.Features.Games;

public sealed record SubmitAnswerCommand(
    Guid GameId,
    Guid QuestionId,
    Guid AnswerOptionId,
    Guid? RoundId,
    Guid? IdempotencyKey) : ICommand<Result<SubmitAnswerResponse>>;

public sealed record SubmitAnswerResponse(bool Correct, int Points, int RoundNumber, string GameStatus);

public sealed class SubmitAnswerValidator : IValidator<SubmitAnswerCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(SubmitAnswerCommand request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.GameId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.GameId), "GameId required."));
        if (request.QuestionId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.QuestionId), "QuestionId required."));
        if (request.AnswerOptionId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.AnswerOptionId), "AnswerOptionId required."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class SubmitAnswerHandler(
    IRepository<Game, GameId> gameRepository,
    IRepository<Question, QuestionId> questionRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<SubmitAnswerCommand, Result<SubmitAnswerResponse>>
{
    // Simple in-memory idempotency store per handler instance (for demo); real implementation would use DB or cache
    private static readonly HashSet<string> _seenKeys = [];

    public async Task<Result<SubmitAnswerResponse>> HandleAsync(SubmitAnswerCommand command, CancellationToken ct)
    {
        var game = await gameRepository.GetByIdAsync(new GameId(command.GameId), ct);
        if (game is null) return Result.Failure<SubmitAnswerResponse>(GameErrors.GameNotFound);

        if (!game.CanSubmitAnswer())
            return Result.Failure<SubmitAnswerResponse>(GameErrors.InvalidGameStateDetail("No active round to submit answer."));

        var round = command.RoundId.HasValue
            ? game.Rounds.FirstOrDefault(r => r.Id.Value == command.RoundId.Value)
            : game.CurrentRound;

        if (round == null) return Result.Failure<SubmitAnswerResponse>(GameErrors.InvalidGameStateDetail("Round not found."));

        // Server timestamp for TimeLimit check
        var elapsed = DateTimeOffset.UtcNow - round.StartedAt;
        if (elapsed.TotalSeconds > game.Configuration.TimeLimitPerQuestionSeconds)
            return Result.Failure<SubmitAnswerResponse>(Error.Validation("AnswerTimeout", "Answer submitted after time limit."));

        // Idempotency: check gameId+roundId+questionId+answerOptionId or provided key
        var key = command.IdempotencyKey?.ToString() ?? $"{command.GameId}:{round.Id.Value}:{command.QuestionId}:{command.AnswerOptionId}";
        lock (_seenKeys)
        {
            if (_seenKeys.Contains(key))
            {
                // Idempotent return (assume previously correct logic, here return dummy)
                return Result.Success(new SubmitAnswerResponse(false, 0, round.RoundNumber, game.Status.Name));
            }
            _seenKeys.Add(key);
        }

        var question = await questionRepository.GetByIdAsync(new QuestionId(command.QuestionId), ct);
        if (question is null) return Result.Failure<SubmitAnswerResponse>(Error.NotFound("QuestionNotFound", "Question not found."));

        var correctOption = question.AnswerOptions.FirstOrDefault(a => a.IsCorrect);
        var isCorrect = correctOption != null && correctOption.Id.Value == command.AnswerOptionId;

        // For demo, points are 10 if correct else 0 (real ledger would create PointTransaction)
        var points = isCorrect ? 10 : 0;

        // In real implementation, create PointTransaction ledger entry here

        // For now, just return
        return Result.Success(new SubmitAnswerResponse(isCorrect, points, round.RoundNumber, game.Status.Name));
    }
}

public sealed class SubmitAnswerEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/games/{id:guid}/answers", async (Guid id, SubmitAnswerCommand body, ISender sender, CancellationToken ct) =>
        {
            var command = body with { GameId = id };
            var result = await sender.SendAsync(command, ct);
            return result.ToHttpResult();
        }).RequireAuthorization(); // PLAYER
    }
}
