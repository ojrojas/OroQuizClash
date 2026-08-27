using BuildingBlocks.CQRS.Abstractions;
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

public sealed record GetRoundQuestionQuery(Guid GameId, Guid RoundId, bool IsPlayer) : IQuery<Result<PresentQuestionResponse>>;

public sealed record PresentQuestionResponse(
    Guid QuestionId,
    string Text,
    Guid CategoryId,
    int Difficulty,
    int TimeLimit,
    int RoundNumber,
    string Status,
    IReadOnlyList<AnswerOptionResponse> AnswerOptions);

public sealed record AnswerOptionResponse(
    Guid Id,
    string Text,
    int DisplayOrder,
    bool? IsCorrect);

public sealed class GetRoundQuestionHandler(
    IRepository<Game, GameId> gameRepository,
    IRepository<Question, QuestionId> questionRepository) : IQueryHandler<GetRoundQuestionQuery, Result<PresentQuestionResponse>>
{
    public async Task<Result<PresentQuestionResponse>> HandleAsync(GetRoundQuestionQuery query, CancellationToken ct)
    {
        var spec = new GameByIdSpecification(new GameId(query.GameId));
        var game = await gameRepository.FirstOrDefaultAsync(spec, ct);
        if (game is null)
            return Result.Failure<PresentQuestionResponse>(GameErrors.GameNotFound);

        var round = game.Rounds.FirstOrDefault(r => r.Id.Value == query.RoundId);
        if (round is null)
            return Result.Failure<PresentQuestionResponse>(GameErrors.InvalidGameStateDetail("Round not found."));

        var question = await questionRepository.GetByIdAsync(round.QuestionId, ct);
        if (question is null)
            return Result.Failure<PresentQuestionResponse>(GameErrors.InvalidGameStateDetail("Question not found."));

        var options = question.AnswerOptions
            .OrderBy(o => o.DisplayOrder)
            .Select(o => new AnswerOptionResponse(
                o.Id.Value,
                o.Text,
                o.DisplayOrder,
                query.IsPlayer ? null : o.IsCorrect))
            .ToList();

        var response = new PresentQuestionResponse(
            question.Id.Value,
            question.Text,
            question.CategoryId.Value,
            round.Difficulty,
            round.TimeLimit,
            round.RoundNumber,
            round.Status.Name,
            options);

        return Result.Success(response);
    }
}

public sealed class GetRoundQuestionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/games/{id:guid}/rounds/{roundId:guid}/question", async (
            Guid id,
            Guid roundId,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var isPlayer = httpContext.User.IsInRole("PLAYER");
            var result = await sender.SendAsync(new GetRoundQuestionQuery(id, roundId, isPlayer), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
