using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using OroQuizClash.Domain.Categories;

namespace OroQuizClash.Application.Features.Categories;

public sealed record PublishCategoryCommand(Guid Id) : ICommand<Result<PublishCategoryResponse>>;

public sealed record PublishCategoryResponse(
    Guid Id,
    string Name,
    string Status,
    string RowVersion);

public sealed class PublishCategoryValidator : IValidator<PublishCategoryCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(PublishCategoryCommand request, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        if (request.Id == Guid.Empty)
            failures.Add(new ValidationFailure(nameof(request.Id), "Id is required."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class PublishCategoryHandler(
    IRepository<Category, CategoryId> repository,
    IUnitOfWork unitOfWork,
    IQuestionCounter questionCounter)
    : ICommandHandler<PublishCategoryCommand, Result<PublishCategoryResponse>>
{
    public async Task<Result<PublishCategoryResponse>> HandleAsync(PublishCategoryCommand command, CancellationToken ct)
    {
        var categoryId = new CategoryId(command.Id);
        var category = await repository.GetByIdAsync(categoryId, ct);
        if (category is null)
            return Result.Failure<PublishCategoryResponse>(CategoryErrors.CategoryNotFound(command.Id));

        var result = await category.PublishAsync(questionCounter, ct);
        if (result.IsFailure)
            return Result.Failure<PublishCategoryResponse>(result.Error);

        repository.Update(category);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PublishCategoryResponse>(CategoryErrors.ConcurrencyConflict);
        }

        var response = new PublishCategoryResponse(
            category.Id.Value,
            category.Name,
            category.Status.Name,
            category.RowVersion != null && category.RowVersion.Length > 0 ? Convert.ToBase64String(category.RowVersion) : string.Empty);

        return Result.Success(response);
    }
}

public sealed class PublishCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/categories/{id:guid}/publish", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new PublishCategoryCommand(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}