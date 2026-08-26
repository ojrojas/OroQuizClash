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

public sealed record ActivateCategoryCommand(Guid Id) : ICommand<Result<ActivateCategoryResponse>>;

public sealed record ActivateCategoryResponse(
    Guid Id,
    string Name,
    string Status,
    string RowVersion);

public sealed class ActivateCategoryValidator : IValidator<ActivateCategoryCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(ActivateCategoryCommand request, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        if (request.Id == Guid.Empty)
            failures.Add(new ValidationFailure(nameof(request.Id), "Id is required."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class ActivateCategoryHandler(IRepository<Category, CategoryId> repository, IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateCategoryCommand, Result<ActivateCategoryResponse>>
{
    public async Task<Result<ActivateCategoryResponse>> HandleAsync(ActivateCategoryCommand command, CancellationToken ct)
    {
        var categoryId = new CategoryId(command.Id);
        var category = await repository.GetByIdAsync(categoryId, ct);
        if (category is null)
            return Result.Failure<ActivateCategoryResponse>(CategoryErrors.CategoryNotFound(command.Id));

        var result = category.Activate();
        if (result.IsFailure)
            return Result.Failure<ActivateCategoryResponse>(result.Error);

        repository.Update(category);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ActivateCategoryResponse>(CategoryErrors.ConcurrencyConflict);
        }

        var response = new ActivateCategoryResponse(
            category.Id.Value,
            category.Name,
            category.Status.Name,
            category.RowVersion != null && category.RowVersion.Length > 0 ? Convert.ToBase64String(category.RowVersion) : string.Empty);

        return Result.Success(response);
    }
}

public sealed class ActivateCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/categories/{id:guid}/activate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new ActivateCategoryCommand(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}