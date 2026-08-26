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

public sealed record DeactivateCategoryCommand(Guid Id) : ICommand<Result<DeactivateCategoryResponse>>;

public sealed record DeactivateCategoryResponse(
    Guid Id,
    string Name,
    string Status,
    string RowVersion);

public sealed class DeactivateCategoryValidator : IValidator<DeactivateCategoryCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(DeactivateCategoryCommand request, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        if (request.Id == Guid.Empty)
            failures.Add(new ValidationFailure(nameof(request.Id), "Id is required."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class DeactivateCategoryHandler(IRepository<Category, CategoryId> repository, IUnitOfWork unitOfWork)
    : ICommandHandler<DeactivateCategoryCommand, Result<DeactivateCategoryResponse>>
{
    public async Task<Result<DeactivateCategoryResponse>> HandleAsync(DeactivateCategoryCommand command, CancellationToken ct)
    {
        var categoryId = new CategoryId(command.Id);
        var category = await repository.GetByIdAsync(categoryId, ct);
        if (category is null)
            return Result.Failure<DeactivateCategoryResponse>(CategoryErrors.CategoryNotFound(command.Id));

        var result = category.Deactivate();
        if (result.IsFailure)
            return Result.Failure<DeactivateCategoryResponse>(result.Error);

        repository.Update(category);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<DeactivateCategoryResponse>(CategoryErrors.ConcurrencyConflict);
        }

        var response = new DeactivateCategoryResponse(
            category.Id.Value,
            category.Name,
            category.Status.Name,
            category.RowVersion != null && category.RowVersion.Length > 0 ? Convert.ToBase64String(category.RowVersion) : string.Empty);

        return Result.Success(response);
    }
}

public sealed class DeactivateCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/categories/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new DeactivateCategoryCommand(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}