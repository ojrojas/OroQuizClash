using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Shared.Errors;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games;

public sealed record GetAnswerQuery(Guid GameId, Guid AnswerId) : IQuery<Result<AnswerDetailResponse>>;

public sealed record AnswerDetailResponse(
    Guid AnswerId,
    Guid GameId,
    Guid PlayerId,
    Guid RoundId,
    Guid QuestionId,
    Guid AnswerOptionId,
    bool? Correct,
    int Points,
    int ElapsedTime,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EvaluatedAt);

public sealed class GetAnswerHandler(IRepository<Game, GameId> repository) : IQueryHandler<GetAnswerQuery, Result<AnswerDetailResponse>>
{
    public async Task<Result<AnswerDetailResponse>> HandleAsync(GetAnswerQuery query, CancellationToken ct)
    {
        var spec = new GameByIdWithAnswersSpecification(new GameId(query.GameId));
        var game = await repository.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<AnswerDetailResponse>(GameErrors.GameNotFound);

        var answer = game.Answers.FirstOrDefault(a => a.Id.Value == query.AnswerId);
        if (answer is null) return Result.Failure<AnswerDetailResponse>(GameErrors.GameNotFound);

        return Result.Success(new AnswerDetailResponse(
            answer.Id.Value,
            answer.GameId.Value,
            answer.PlayerId,
            answer.RoundId.Value,
            answer.QuestionId.Value,
            answer.AnswerOptionId.Value,
            answer.Correct,
            answer.Points,
            answer.ElapsedTime,
            answer.Status.Name,
            answer.CreatedAt,
            answer.EvaluatedAt));
    }
}

public sealed class GetAnswerEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/games/{id:guid}/answers/{answerId:guid}", async (
            Guid id,
            Guid answerId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetAnswerQuery(id, answerId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
