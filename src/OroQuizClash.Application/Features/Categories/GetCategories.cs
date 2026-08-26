using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Categories;

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int DifficultyLevel,
    IReadOnlyCollection<string> Tags,
    string Status,
    int ValidQuestionsCount,
    string RowVersion);

public sealed record PaginatedResponse(
    IReadOnlyList<CategoryResponse> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record GetCategoriesQuery(
    string? KnowledgeArea,
    string? AcademicLevel,
    int? AgeMin,
    int? AgeMax,
    int? DifficultyLevel,
    string? State,
    string? Tag,
    int Page = 1,
    int PageSize = 20) : IQuery<Result<PaginatedResponse>>;

public sealed class GetCategoriesValidator : IValidator<GetCategoriesQuery>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        if (request.Page < 1)
            failures.Add(new ValidationFailure(nameof(request.Page), "Page must be >= 1."));
        if (request.PageSize < 1 || request.PageSize > 100)
            failures.Add(new ValidationFailure(nameof(request.PageSize), "PageSize must be 1-100."));
        if (request.AgeMin.HasValue && (request.AgeMin < 0 || request.AgeMin > 120))
            failures.Add(new ValidationFailure(nameof(request.AgeMin), "AgeMin must be 0-120."));
        if (request.AgeMax.HasValue && (request.AgeMax < 0 || request.AgeMax > 120))
            failures.Add(new ValidationFailure(nameof(request.AgeMax), "AgeMax must be 0-120."));
        if (request.AgeMin.HasValue && request.AgeMax.HasValue && request.AgeMin > request.AgeMax)
            failures.Add(new ValidationFailure(nameof(request.AgeMin), "AgeMin must be <= AgeMax."));
        if (request.DifficultyLevel.HasValue && (request.DifficultyLevel < 1 || request.DifficultyLevel > 5))
            failures.Add(new ValidationFailure(nameof(request.DifficultyLevel), "DifficultyLevel must be 1-5."));
        if (request.State != null)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DRAFT", "ACTIVE", "INACTIVE", "ARCHIVED" };
            if (!allowed.Contains(request.State.Trim()))
                failures.Add(new ValidationFailure(nameof(request.State), $"State must be one of {string.Join(",", allowed)}."));
        }
        if (request.Tag != null && (request.Tag.Trim().Length < 2 || request.Tag.Trim().Length > 30))
            failures.Add(new ValidationFailure(nameof(request.Tag), "Tag must be 2-30 characters."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class GetCategoriesHandler(
    IRepository<Category, CategoryId> repository,
    IQuestionCounter questionCounter) : IQueryHandler<GetCategoriesQuery, Result<PaginatedResponse>>
{
    public async Task<Result<PaginatedResponse>> HandleAsync(GetCategoriesQuery query, CancellationToken ct)
    {
        var countSpec = CategoryFilterSpecification.ForCount(
            query.KnowledgeArea,
            query.AcademicLevel,
            query.DifficultyLevel,
            query.State,
            query.Tag,
            query.AgeMin,
            query.AgeMax);

        var listSpec = new CategoryFilterSpecification(
            query.KnowledgeArea,
            query.AcademicLevel,
            query.DifficultyLevel,
            query.State,
            query.Tag,
            query.AgeMin,
            query.AgeMax,
            query.Page,
            query.PageSize);

        var total = await repository.CountAsync(countSpec, ct);
        var categories = await repository.ListAsync(listSpec, ct);

        var items = new List<CategoryResponse>(categories.Count);
        foreach (var cat in categories)
        {
            var count = await questionCounter.CountValidAsync(cat.Id, ct);
            items.Add(Map(cat, count));
        }

        var response = new PaginatedResponse(items, total, query.Page, query.PageSize);
        return Result.Success(response);
    }

    internal static CategoryResponse Map(Category cat, int validCount) =>
        new(
            cat.Id.Value,
            cat.Name,
            cat.Description,
            cat.KnowledgeArea.Value,
            cat.AcademicLevel.Value,
            cat.AgeRange.Min,
            cat.AgeRange.Max,
            cat.DifficultyLevel.Value,
            cat.Tags.Tags.ToList(),
            cat.Status.Name,
            validCount,
            cat.RowVersion != null && cat.RowVersion.Length > 0 ? Convert.ToBase64String(cat.RowVersion) : string.Empty);
}

public sealed class GetCategoriesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories", async (
            string? knowledgeArea,
            string? academicLevel,
            int? ageMin,
            int? ageMax,
            int? difficultyLevel,
            string? state,
            string? tag,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetCategoriesQuery(
                knowledgeArea,
                academicLevel,
                ageMin,
                ageMax,
                difficultyLevel,
                state,
                tag,
                page ?? 1,
                pageSize ?? 20);
            var result = await sender.SendAsync(query, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}