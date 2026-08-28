using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Questions;

public sealed record GetQuestionsQuery(
    Guid? CategoryId,
    int? Difficulty,
    string? AcademicLevel,
    int? AgeMin,
    int? AgeMax,
    string? Status,
    string? Search,
    int Page = 1,
    int PageSize = 20) : IQuery<Result<PaginatedQuestionsResponse>>;

public sealed record PaginatedQuestionsResponse(
    IReadOnlyList<CreateQuestionResponse> Items,
    int Total,
    int Page,
    int PageSize);

public sealed class GetQuestionsHandler(IRepository<Question, QuestionId> repository)
    : IQueryHandler<GetQuestionsQuery, Result<PaginatedQuestionsResponse>>
{
    public async Task<Result<PaginatedQuestionsResponse>> HandleAsync(GetQuestionsQuery query, CancellationToken ct)
    {
        CategoryId? catId = query.CategoryId.HasValue ? new CategoryId(query.CategoryId.Value) : null;

        var spec = new QuestionFilterSpecification(catId, query.Difficulty, query.AcademicLevel, query.AgeMin, query.AgeMax, query.Status, query.Search, query.Page, query.PageSize, paginate: true);
        var countSpec = new QuestionFilterSpecification(catId, query.Difficulty, query.AcademicLevel, query.AgeMin, query.AgeMax, query.Status, query.Search, 1, 20, paginate: false);

        var items = await repository.ListAsync(spec, ct);
        var total = await repository.CountAsync(countSpec, ct);

        var mapped = items.Select(q => new CreateQuestionResponse(
            q.Id.Value, q.Text, q.CategoryId.Value, q.Difficulty.Id, q.AcademicLevel.Value, q.AgeRange.Min, q.AgeRange.Max,
            q.Status.Name,
            q.AnswerOptions.Select(a => new AnswerOptionResponse(a.Id.Value, a.Text, a.IsCorrect, a.DisplayOrder)).ToList(),
            q.RowVersion != null && q.RowVersion.Length > 0 ? Convert.ToBase64String(q.RowVersion) : string.Empty,
            q.CreatedAt)).ToList();

        return Result.Success(new PaginatedQuestionsResponse(mapped, total, query.Page, query.PageSize));
    }
}

public sealed class GetQuestionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/questions", async (
            Guid? categoryId,
            int? difficulty,
            string? academicLevel,
            int? ageMin,
            int? ageMax,
            string? status,
            string? search,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetQuestionsQuery(categoryId, difficulty, academicLevel, ageMin, ageMax, status, search, page ?? 1, pageSize ?? 20);
            var result = await sender.SendAsync(query, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record GetQuestionByIdQuery(Guid Id) : IQuery<Result<CreateQuestionResponse>>;

public sealed class GetQuestionByIdHandler(IRepository<Question, QuestionId> repository)
    : IQueryHandler<GetQuestionByIdQuery, Result<CreateQuestionResponse>>
{
    public async Task<Result<CreateQuestionResponse>> HandleAsync(GetQuestionByIdQuery query, CancellationToken ct)
    {
        var question = await repository.FirstOrDefaultAsync(
            new QuestionByIdSpecification(new QuestionId(query.Id)), ct);
        if (question is null)
            return Result.Failure<CreateQuestionResponse>(QuestionErrors.QuestionNotFound(query.Id));

        var response = new CreateQuestionResponse(
            question.Id.Value, question.Text, question.CategoryId.Value, question.Difficulty.Id,
            question.AcademicLevel.Value, question.AgeRange.Min, question.AgeRange.Max,
            question.Status.Name,
            question.AnswerOptions.Select(a => new AnswerOptionResponse(a.Id.Value, a.Text, a.IsCorrect, a.DisplayOrder)).ToList(),
            question.RowVersion != null && question.RowVersion.Length > 0 ? Convert.ToBase64String(question.RowVersion) : string.Empty,
            question.CreatedAt);
        return Result.Success(response);
    }
}

public sealed class GetQuestionByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/questions/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetQuestionByIdQuery(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
