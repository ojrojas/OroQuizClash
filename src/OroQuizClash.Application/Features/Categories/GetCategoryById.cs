using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Categories;

public sealed record GetCategoryByIdQuery(CategoryId Id) : IQuery<Result<CategoryResponse>>
{
    public GetCategoryByIdQuery(Guid id) : this(new CategoryId(id)) { }
}

public sealed class GetCategoryByIdValidator : IValidator<GetCategoryByIdQuery>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        if (request.Id.Value == Guid.Empty)
            failures.Add(new ValidationFailure(nameof(request.Id), "Id is required."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class GetCategoryByIdHandler(
    IRepository<Category, CategoryId> repository,
    IQuestionCounter questionCounter) : IQueryHandler<GetCategoryByIdQuery, Result<CategoryResponse>>
{
    public async Task<Result<CategoryResponse>> HandleAsync(GetCategoryByIdQuery query, CancellationToken ct)
    {
        var spec = new CategoryByIdSpecification(query.Id);
        var category = await repository.FirstOrDefaultAsync(spec, ct);
        if (category is null)
            return Result.Failure<CategoryResponse>(CategoryErrors.CategoryNotFound(query.Id.Value));

        var count = await questionCounter.CountValidAsync(category.Id, ct);
        var response = new CategoryResponse(
            category.Id.Value,
            category.Name,
            category.Description,
            category.KnowledgeArea.Value,
            category.AcademicLevel.Value,
            category.AgeRange.Min,
            category.AgeRange.Max,
            category.DifficultyLevel.Value,
            category.Tags.Tags.ToList(),
            category.Status.Name,
            count,
            category.RowVersion != null && category.RowVersion.Length > 0 ? Convert.ToBase64String(category.RowVersion) : string.Empty);

        return Result.Success(response);
    }
}

public sealed class GetCategoryByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetCategoryByIdQuery(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}