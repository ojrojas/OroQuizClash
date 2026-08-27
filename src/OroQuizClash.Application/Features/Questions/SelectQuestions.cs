using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.Services;
using OroQuizClash.Domain.Questions.ValueObjects;

namespace OroQuizClash.Application.Features.Questions;

public sealed record SelectQuestionsRequest(
    Guid? CategoryId,
    int? Difficulty,
    string? AcademicLevel,
    int? AgeMin,
    int? AgeMax,
    List<Guid>? PreviousQuestionIds,
    Guid GameId,
    int? RoundNumber,
    Guid? RoundId,
    int Take = 1);

public sealed record SelectQuestionsResponse(
    IReadOnlyList<CreateQuestionResponse> Items,
    string Strategy);

public sealed record SelectQuestionsQuery(SelectQuestionsRequest Request) : IQuery<Result<SelectQuestionsResponse>>;

public sealed class SelectQuestionsHandler(IQuestionSelectionStrategy selectionStrategy) : IQueryHandler<SelectQuestionsQuery, Result<SelectQuestionsResponse>>
{
    public async Task<Result<SelectQuestionsResponse>> HandleAsync(SelectQuestionsQuery query, CancellationToken ct)
    {
        var req = query.Request;

        if (req.GameId == Guid.Empty)
            return Result.Failure<SelectQuestionsResponse>(Error.Validation("Select.GameIdRequired", "GameId is required."));

        CategoryId? catId = req.CategoryId.HasValue ? new CategoryId(req.CategoryId.Value) : null;
        DifficultyLevel? diff = null;
        if (req.Difficulty.HasValue)
        {
            try { diff = DifficultyLevel.FromId(req.Difficulty.Value); }
            catch { return Result.Failure<SelectQuestionsResponse>(Error.Validation("Select.InvalidDifficulty", "Difficulty must be 1-5.")); }
        }

        AgeRange? ageRange = null;
        if (req.AgeMin.HasValue || req.AgeMax.HasValue)
        {
            var min = req.AgeMin ?? 0;
            var max = req.AgeMax ?? 120;
            try { ageRange = new AgeRange(min, max); }
            catch (Exception ex) { return Result.Failure<SelectQuestionsResponse>(Error.Validation("Select.InvalidAgeRange", ex.Message)); }
        }

        var previous = (req.PreviousQuestionIds ?? []).Select(id => new QuestionId(id)).ToList();

        var criteria = new QuestionSelectionCriteria(
            catId, diff, req.AcademicLevel, ageRange, previous, req.GameId, req.RoundNumber, req.RoundId, req.Take);

        var result = await selectionStrategy.SelectAsync(criteria, ct);
        if (result.IsFailure)
            return Result.Failure<SelectQuestionsResponse>(result.Error);

        var mapped = result.Value.Select(q => new CreateQuestionResponse(
            q.Id.Value, q.Text, q.CategoryId.Value, q.Difficulty.Id,
            q.AcademicLevel.Value, q.AgeRange.Min, q.AgeRange.Max,
            q.Status.Name,
            q.AnswerOptions.Select(a => new AnswerOptionResponse(a.Id.Value, a.Text, a.IsCorrect, a.DisplayOrder)).ToList(),
            q.RowVersion != null && q.RowVersion.Length > 0 ? Convert.ToBase64String(q.RowVersion) : string.Empty,
            q.CreatedAt)).ToList();

        return Result.Success(new SelectQuestionsResponse(mapped, selectionStrategy.GetType().Name.Replace("QuestionSelectionStrategy", string.Empty)));
    }
}

public sealed class SelectQuestionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/questions/select", async (SelectQuestionsRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new SelectQuestionsQuery(request), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();

        // Alternative Game-scoped endpoint per contract question-selection.openapi.yaml
        app.MapGet("/api/games/{gameId:guid}/rounds/{roundNumber:int}/question", async (
            Guid gameId, int roundNumber, Guid? categoryId, int? difficulty, string? academicLevel, int? ageMin, int? ageMax, ISender sender, CancellationToken ct) =>
        {
            var req = new SelectQuestionsRequest(categoryId, difficulty, academicLevel, ageMin, ageMax, [], gameId, roundNumber, null, 1);
            var result = await sender.SendAsync(new SelectQuestionsQuery(req), ct);
            if (result.IsFailure) return result.ToHttpResult();
            // Return single item for GET convenience
            var single = result.Value.Items.FirstOrDefault();
            return single is null ? Results.NotFound() : Results.Ok(single);
        }).RequireAuthorization();

        app.MapPost("/api/games/{gameId:guid}/questions/select", async (Guid gameId, SelectQuestionsRequest request, ISender sender, CancellationToken ct) =>
        {
            var enriched = request with { GameId = gameId };
            var result = await sender.SendAsync(new SelectQuestionsQuery(enriched), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
